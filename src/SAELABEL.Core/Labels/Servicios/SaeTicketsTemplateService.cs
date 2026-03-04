using System.Xml.Linq;
using SAELABEL.Core.Labels.Helpers;
using SAELABEL.Core.Labels.Modelos;

namespace SAELABEL.Core.Labels.Servicios;

public class SaeTicketsTemplateService
{
    public byte[] ProcessTicketXml(string xml, Dictionary<string, string> data, int paperWidthOverride = 0)
    {
        var doc = XDocument.Parse(xml);
        var root = doc.Root;
        if (root?.Name.LocalName != "saetickets")
            throw new InvalidDataException("El XML no es un formato saetickets válido.");

        var setup = root.Element("setup");
        int width = paperWidthOverride > 0
            ? (paperWidthOverride == 58 ? 32 : 42)
            : ((int?)setup?.Attribute("width") ?? 42);

        var builder = new TicketBuilder(width);

        var commands = root.Element("commands")?.Elements();
        if (commands == null) return builder.Build();

        foreach (var cmd in commands)
            ProcessCmd(cmd, builder, data, width);

        return builder.Build();
    }

    private void ProcessCmd(XElement cmd, TicketBuilder builder, Dictionary<string, string> data, int width)
    {
        // showIf: skip element if condition is falsy
        var showIf = cmd.Attribute("showIf")?.Value;
        if (!string.IsNullOrEmpty(showIf) && IsFalsy(ReplaceVars(showIf, data)))
            return;

        switch (cmd.Name.LocalName)
        {
            case "text":
            {
                builder.SetAlignment(ParseAlign(cmd.Attribute("align")?.Value));
                RenderRichText(builder, ReplaceVars(cmd.Value, data), (bool?)cmd.Attribute("bold") ?? false, ParseFontSize(cmd.Attribute("size")?.Value));
                builder.NewLine();
                break;
            }

            case "separator":
            {
                char c = (cmd.Attribute("char")?.Value ?? "-")[0];
                builder.Separator(c);
                break;
            }

            case "item":
            {
                builder.Item(
                    ReplaceVars(cmd.Attribute("description")?.Value ?? "", data),
                    ReplaceVars(cmd.Attribute("quantity")?.Value  ?? "1", data),
                    ReplaceVars(cmd.Attribute("price")?.Value,  data),
                    ReplaceVars(cmd.Attribute("total")?.Value,  data));
                break;
            }

            case "total":
            {
                var label = ReplaceVars(cmd.Attribute("label")?.Value ?? "TOTAL", data);
                var value = ReplaceVars(cmd.Attribute("value")?.Value ?? "0",     data);
                bool bold = (bool?)cmd.Attribute("bold") ?? false;
                builder.SetAlignment(TicketAlignment.Left);
                RenderRichText(builder, PadStr(label, width - value.Length, TicketAlignment.Left, PrinterFontSize.Normal), bold, PrinterFontSize.Normal);
                RenderRichText(builder, value, bold, PrinterFontSize.Normal);
                builder.NewLine();
                break;
            }

            case "qr":
            {
                var sizeAttr = cmd.Attribute("size")?.Value;
                int moduleSize = 6;
                if (int.TryParse(sizeAttr, out var szVal))
                    moduleSize = szVal > 20 ? Math.Max(1, Math.Min(16, szVal / 16)) : szVal;

                builder.QrCode(ReplaceVars(cmd.Value, data), ParseAlign(cmd.Attribute("align")?.Value), moduleSize);
                break;
            }

            case "feed":
            {
                builder.Feed((int?)cmd.Attribute("lines") ?? 1);
                break;
            }

            case "cut":         builder.Cut();        break;
            case "beep":        builder.Beep();       break;
            case "open-drawer": builder.OpenDrawer(); break;

            case "if":
            {
                var expr = ReplaceVars(cmd.Attribute("expr")?.Value ?? "", data);
                if (IsFalsy(expr)) break;
                builder.SetAlignment(ParseAlign(cmd.Attribute("align")?.Value));
                RenderRichText(builder, ReplaceVars(cmd.Value, data), (bool?)cmd.Attribute("bold") ?? false, PrinterFontSize.Normal);
                builder.NewLine();
                break;
            }

            case "ifelse":
            {
                var expr  = ReplaceVars(cmd.Attribute("expr")?.Value ?? "", data);
                var align = ParseAlign(cmd.Attribute("align")?.Value);
                var cmdElem = !IsFalsy(expr) ? cmd.Element("then") : cmd.Element("else");
                if (cmdElem != null)
                {
                    builder.SetAlignment(align);
                    RenderRichText(builder, ReplaceVars(cmdElem.Value, data), false, PrinterFontSize.Normal);
                    builder.NewLine();
                }
                break;
            }

            case "each":
            {
                var listVar = cmd.Attribute("listVar")?.Value ?? "ITEMS";
                bool header = cmd.Attribute("header")?.Value?.ToLower() != "false";
                var childField = cmd.Attribute("childField")?.Value;
                int childIndentCol = (int?)cmd.Attribute("childIndentCol") ?? 0;

                var cols = cmd.Elements("column").Select(c => new
                {
                    Field  = c.Attribute("field")?.Value ?? "",
                    Label  = c.Attribute("label")?.Value ?? "",
                    WidthS = c.Attribute("width")?.Value ?? "auto",
                    Align  = ParseAlign(c.Attribute("align")?.Value),
                    ShowIf = c.Attribute("showIf")?.Value,
                    Bold   = (bool?)c.Attribute("bold") ?? false,
                    Size   = ParseFontSize(c.Attribute("size")?.Value)
                }).ToList();

                int sep    = Math.Max(0, cols.Count - 1);
                int fixedW = cols.Where(c => c.WidthS != "auto").Sum(c => int.TryParse(c.WidthS, out var w) ? w : 0);
                int autoN  = cols.Count(c => c.WidthS == "auto");
                int autoW  = autoN > 0 ? Math.Max(1, (width - fixedW - sep) / autoN) : 0;
                var widths = cols.Select(c => c.WidthS == "auto" ? autoW : (int.TryParse(c.WidthS, out var w2) ? w2 : autoW)).ToList();

                // Determine row count
                int count = 0;
                if (data.TryGetValue($"{listVar}_COUNT", out var cStr) && int.TryParse(cStr, out var cParsed))
                    count = cParsed;
                else
                {
                    var firstField = cols.FirstOrDefault()?.Field ?? "";
                    while (data.ContainsKey($"{listVar}_{count}_{firstField}")) count++;
                }

                if (count == 0) break;

                // Header row
                if (header)
                {
                    builder.SetAlignment(TicketAlignment.Left);
                    for (int i = 0; i < cols.Count; i++)
                    {
                        var c = cols[i];
                        builder.TextPart(PadStr(c.Label, widths[i], c.Align), true);
                        if (i < cols.Count - 1) builder.TextPart(" ");
                    }
                    builder.NewLine();
                    builder.Separator('-');
                }

                // Data rows
                for (int i = 0; i < count; i++)
                {
                    var rowData = new Dictionary<string, string>(data);
                    foreach (var col in cols)
                    {
                        var k = $"{listVar}_{i}_{col.Field}";
                        if (data.TryGetValue(k, out var v)) rowData[col.Field] = v;
                    }
                    if (childField != null && data.TryGetValue($"{listVar}_{i}_{childField}", out var cv))
                        rowData[childField] = cv;

                    builder.SetAlignment(TicketAlignment.Left);
                    for (int ci = 0; ci < cols.Count; ci++)
                    {
                        var col = cols[ci];
                        if (!string.IsNullOrEmpty(col.ShowIf) && IsFalsy(ReplaceVars(col.ShowIf, rowData)))
                        {
                            builder.TextPart(new string(' ', widths[ci]));
                        }
                        else
                        {
                            var val = rowData.TryGetValue($"{listVar}_{i}_{col.Field}", out var fv) ? fv : "";
                            RenderRichText(builder, PadStr(ReplaceVars(val, rowData), widths[ci], col.Align, col.Size), col.Bold, col.Size);
                        }
                        if (ci < cols.Count - 1) builder.TextPart(" ");
                    }
                    builder.NewLine();

                    // Optional child field row - split by comma for multi-line
                    if (!string.IsNullOrEmpty(childField) && rowData.TryGetValue(childField, out var childVal) && !string.IsNullOrEmpty(childVal))
                    {
                        var parts = childVal.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        int indent = widths.Take(childIndentCol).Sum() + childIndentCol;
                        foreach (var part in parts)
                        {
                            builder.SetAlignment(TicketAlignment.Left);
                            builder.TextPart(new string(' ', indent));
                            RenderRichText(builder, ReplaceVars(part, rowData), false, PrinterFontSize.Normal);
                            builder.NewLine();
                        }
                    }
                }
                break;
            }
        }
    }

    // ─── Static helpers ───────────────────────────────────────────────────────

    private void RenderRichText(TicketBuilder builder, string text, bool baseBold, PrinterFontSize size, bool extraBold = false)
    {
        if (string.IsNullOrEmpty(text)) return;
        
        string pattern = @"(####[^#]+####|###[^#]+###|##[^#]+##|\*\*\*[^*]+\*\*\*|\*\*[^*]+\*\*)";
        var match = System.Text.RegularExpressions.Regex.Match(text, pattern);

        if (!match.Success)
        {
            builder.TextPart(text, baseBold, size, extraBold);
            return;
        }

        // Render prefix
        if (match.Index > 0)
            builder.TextPart(text.Substring(0, match.Index), baseBold, size, extraBold);

        // Apply tag and recurse
        string tag = match.Value;
        if (tag.StartsWith("####"))
            RenderRichText(builder, tag.Substring(4, tag.Length - 8), baseBold, PrinterFontSize.ExtraLarge, extraBold);
        else if (tag.StartsWith("###"))
            RenderRichText(builder, tag.Substring(3, tag.Length - 6), baseBold, PrinterFontSize.Large, extraBold);
        else if (tag.StartsWith("##"))
            RenderRichText(builder, tag.Substring(2, tag.Length - 4), baseBold, PrinterFontSize.Medium, extraBold);
        else if (tag.StartsWith("***"))
            RenderRichText(builder, tag.Substring(3, tag.Length - 6), true, size, true);
        else if (tag.StartsWith("**"))
            RenderRichText(builder, tag.Substring(2, tag.Length - 4), true, size, extraBold);

        // Render suffix
        string suffix = text.Substring(match.Index + match.Length);
        if (!string.IsNullOrEmpty(suffix))
            RenderRichText(builder, suffix, baseBold, size, extraBold);
    }

    private static int GetRealLength(string s, PrinterFontSize size = PrinterFontSize.Normal)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        
        string pattern = @"(####[^#]+####|###[^#]+###|##[^#]+##|\*\*\*[^*]+\*\*\*|\*\*[^*]+\*\*)";
        var match = System.Text.RegularExpressions.Regex.Match(s, pattern);

        if (!match.Success)
        {
            int multiplier = (size == PrinterFontSize.Large || size == PrinterFontSize.ExtraLarge) ? 2 : 1;
            return s.Length * multiplier;
        }

        int total = GetRealLength(s.Substring(0, match.Index), size);

        string tag = match.Value;
        if (tag.StartsWith("####"))
            total += GetRealLength(tag.Substring(4, tag.Length - 8), PrinterFontSize.ExtraLarge);
        else if (tag.StartsWith("###"))
            total += GetRealLength(tag.Substring(3, tag.Length - 6), PrinterFontSize.Large);
        else if (tag.StartsWith("##"))
            total += GetRealLength(tag.Substring(2, tag.Length - 4), PrinterFontSize.Medium);
        else if (tag.StartsWith("***"))
            total += GetRealLength(tag.Substring(3, tag.Length - 6), size);
        else if (tag.StartsWith("**"))
            total += GetRealLength(tag.Substring(2, tag.Length - 4), size);

        total += GetRealLength(s.Substring(match.Index + match.Length), size);
        return total;
    }

    private static string PadStr(string s, int w, TicketAlignment a, PrinterFontSize size = PrinterFontSize.Normal)
    {
        int realLen = GetRealLength(s, size);
        if (realLen >= w) return s;
        
        int diff = w - realLen;
        return a switch
        {
            TicketAlignment.Center => new string(' ', diff / 2) + s + new string(' ', diff - (diff / 2)),
            TicketAlignment.Right  => new string(' ', diff) + s,
            _                      => s + new string(' ', diff),
        };
    }

    private static bool IsFalsy(string? val) =>
        string.IsNullOrWhiteSpace(val)
        || val is "0" or "false" or "False" or "no" or "No"
        || val.StartsWith("${");

    private string ReplaceVars(string? input, Dictionary<string, string> data)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var now = DateTime.Now;
        return System.Text.RegularExpressions.Regex.Replace(input, @"\$\{([^}]+)\}", m =>
        {
            var key = m.Groups[1].Value;
            if (key.StartsWith("!"))
            {
                var lowerKey = key.ToLower();
                
                // Support for ${!date:format}
                if (lowerKey.StartsWith("!date:") && key.Length > 6)
                {
                    var format = key.Substring(6);
                    try { return now.ToString(format); } catch { return m.Value; }
                }

                return lowerKey switch
                {
                    "!date"     => now.ToString("yyyy-MM-dd"),
                    "!time"     => now.ToString("HH:mm:ss"),
                    "!datetime" => now.ToString("yyyy-MM-dd HH:mm:ss"),
                    "!year"     => now.ToString("yyyy"),
                    "!month"    => now.ToString("MM"),
                    "!day"      => now.ToString("dd"),
                    "!dayname"  => now.ToString("dddd"),
                    "!daynameshort" => now.ToString("ddd"),
                    "!weekyear" => System.Globalization.ISOWeek.GetWeekOfYear(now).ToString(),
                    "!weekmonth"=> ((now.Day - 1) / 7 + 1).ToString(),
                    _           => m.Value
                };
            }
            return data.TryGetValue(key, out var v) ? v : m.Value;
        });
    }

    private TicketAlignment ParseAlign(string? val) => val?.ToLower() switch
    {
        "center" => TicketAlignment.Center,
        "right"  => TicketAlignment.Right,
        _        => TicketAlignment.Left
    };

    private PrinterFontSize ParseFontSize(string? val) => val?.ToLower() switch
    {
        "medium"      => PrinterFontSize.Medium,
        "large"       => PrinterFontSize.Large,
        "extra-large" => PrinterFontSize.ExtraLarge,
        _             => PrinterFontSize.Normal
    };
}
