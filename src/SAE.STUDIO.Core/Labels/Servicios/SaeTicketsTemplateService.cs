using System.Xml.Linq;
using SAE.STUDIO.Core.Labels.Helpers;
using SAE.STUDIO.Core.Labels.Modelos;

namespace SAE.STUDIO.Core.Labels.Servicios;

public class SaeTicketsTemplateService
{
    public byte[] ProcessTicketXml(string xml, Dictionary<string, string> data, int paperWidthOverride = 0)
    {
        var doc = XDocument.Parse(xml);
        var root = doc.Root;
        if (root?.Name.LocalName != "saetickets")
            throw new InvalidDataException("El XML no es un formato saetickets válido.");

        var setup = root.Element("setup");
        int width;
        if (paperWidthOverride > 0)
        {
            // If we have an override (mm), map it to char count
            // 58mm-60mm range -> 32 chars, else 42 chars
            width = paperWidthOverride <= 65 ? 32 : 42;
        }
        else
        {
            // Use XML setup width (which is character count)
            width = (int?)setup?.Attribute("width") ?? 42;
        }

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
                var alignment = ParseAlign(cmd.Attribute("align")?.Value);
                var isBold = (bool?)cmd.Attribute("bold") ?? false;
                var fontSize = ParseFontSize(cmd.Attribute("size")?.Value);
                var isExtraBold = (bool?)cmd.Attribute("extraBold") ?? false;
                var text = ReplaceVars(cmd.Value, data);

                // Simple word wrap implementation for rich text (ignoring tags for splitting, but respecting their width)
                // For now, we split by lines and then wrap each line.
                var rawLines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                foreach (var rl in rawLines)
                {
                    var lines = WrapLine(rl, width, fontSize);
                    foreach (var line in lines)
                    {
                        RenderRichText(builder, PadStr(line, width, alignment, fontSize), isBold, fontSize, isExtraBold);
                        builder.NewLine();
                    }
                }
                break;
            }

            case "separator":
            {
                var align = ParseAlign(cmd.Attribute("align")?.Value);
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
                bool extraBold = (bool?)cmd.Attribute("extraBold") ?? false;
                var align = ParseAlign(cmd.Attribute("align")?.Value);
                
                if (align == TicketAlignment.Right || align == TicketAlignment.Center)
                {
                    // Render as a single adjacent string for Right/Center alignments
                    string combined = $"{label.TrimEnd()} {value.TrimStart()}";
                    var lines = WrapLine(combined, width, PrinterFontSize.Normal);
                    foreach (var line in lines)
                    {
                        RenderRichText(builder, PadStr(line, width, align, PrinterFontSize.Normal), bold, PrinterFontSize.Normal, extraBold);
                        builder.NewLine();
                    }
                }
                else
                {
                    // Default / Left alignment: Spread label and value to opposite sides
                    int valueLen = GetRealLength(value, PrinterFontSize.Normal);
                    int labelWidth = width - valueLen;
                    if (labelWidth < 5) labelWidth = 5; // minimum label space

                    var labelLines = WrapLine(label, labelWidth, PrinterFontSize.Normal);

                    for (int i = 0; i < labelLines.Count; i++)
                    {
                        string line = labelLines[i];
                        if (i == 0)
                        {
                            RenderRichText(builder, PadStr(line, labelWidth, TicketAlignment.Left, PrinterFontSize.Normal), bold, PrinterFontSize.Normal, extraBold);
                            RenderRichText(builder, value, bold, PrinterFontSize.Normal, extraBold);
                        }
                        else
                        {
                            RenderRichText(builder, PadStr(line, width, TicketAlignment.Left, PrinterFontSize.Normal), bold, PrinterFontSize.Normal, extraBold);
                        }
                        builder.NewLine();
                    }
                }
                break;
            }

            case "qr":
            {
                var sizeAttr = cmd.Attribute("size")?.Value;
                int moduleSize = 6;
                if (int.TryParse(sizeAttr, out var szVal))
                    moduleSize = szVal > 20 ? Math.Max(1, Math.Min(16, szVal / 16)) : szVal;

                var alignValue = cmd.Attribute("align")?.Value;
                var qrAlign = string.IsNullOrEmpty(alignValue) ? TicketAlignment.Center : ParseAlign(alignValue);

                builder.QrCode(ReplaceVars(cmd.Value, data), qrAlign, moduleSize);
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
                var align = ParseAlign(cmd.Attribute("align")?.Value);
                var text = ReplaceVars(cmd.Value, data);
                RenderRichText(builder, PadStr(text, width, align, PrinterFontSize.Normal), (bool?)cmd.Attribute("bold") ?? false, PrinterFontSize.Normal, (bool?)cmd.Attribute("extraBold") ?? false);
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
                    var text = ReplaceVars(cmdElem.Value, data);
                    RenderRichText(builder, PadStr(text, width, align, PrinterFontSize.Normal), false, PrinterFontSize.Normal, (bool?)cmd.Attribute("extraBold") ?? false);
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
                    ExtraBold = (bool?)c.Attribute("extraBold") ?? false,
                    Size   = ParseFontSize(c.Attribute("size")?.Value)
                }).ToList();

                var align = ParseAlign(cmd.Attribute("align")?.Value);
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

                    // Wrap each column's text
                    var columnWrappedLines = new List<List<string>>();
                    int maxLinesInRow = 1;

                    for (int ci = 0; ci < cols.Count; ci++)
                    {
                        var col = cols[ci];
                        string val = "";
                        if (string.IsNullOrEmpty(col.ShowIf) || !IsFalsy(ReplaceVars(col.ShowIf, rowData)))
                        {
                            val = ReplaceVars(rowData.TryGetValue($"{listVar}_{i}_{col.Field}", out var fv) ? fv : "", rowData);
                        }
                        
                        var wrapped = WrapLine(val, widths[ci], col.Size);
                        columnWrappedLines.Add(wrapped);
                        if (wrapped.Count > maxLinesInRow) maxLinesInRow = wrapped.Count;
                    }

                    // Render all lines for this row
                    for (int lineIdx = 0; lineIdx < maxLinesInRow; lineIdx++)
                    {
                        for (int ci = 0; ci < cols.Count; ci++)
                        {
                            var col = cols[ci];
                            var lines = columnWrappedLines[ci];
                            var lineText = lineIdx < lines.Count ? lines[lineIdx] : "";
                            
                            RenderRichText(builder, PadStr(lineText, widths[ci], col.Align, col.Size), col.Bold, col.Size, col.ExtraBold);
                            if (ci < cols.Count - 1) builder.TextPart(" ");
                        }
                        builder.NewLine();
                    }

                    // Optional child field row
                    if (!string.IsNullOrEmpty(childField) && rowData.TryGetValue(childField, out var childVal) && !string.IsNullOrEmpty(childVal))
                    {
                        var parts = childVal.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        int indent = widths.Take(childIndentCol).Sum() + childIndentCol;
                        foreach (var part in parts)
                        {
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

    private List<string> WrapLine(string text, int maxWidth, PrinterFontSize baseSize)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(text))
        {
            result.Add("");
            return result;
        }

        // Very basic wrap: we look for spaces but if a word is too long we cut it.
        // We simplified it by not perfectly splitting rich tags mid-word, 
        // but it's much better than overflowing.
        string currentLine = "";
        int currentLineLen = 0;
        var words = text.Split(' ');

        foreach (var word in words)
        {
            int wordLen = GetRealLength(word, baseSize);
            int spaceLen = currentLineLen > 0 ? GetRealLength(" ", baseSize) : 0;

            if (currentLineLen + spaceLen + wordLen <= maxWidth)
            {
                if (currentLineLen > 0)
                {
                    currentLine += " ";
                    currentLineLen += spaceLen;
                }
                currentLine += word;
                currentLineLen += wordLen;
            }
            else
            {
                if (!string.IsNullOrEmpty(currentLine))
                    result.Add(currentLine);
                
                // If the word itself is longer than maxWidth, we must force wrap it
                if (wordLen > maxWidth)
                {
                    string remainingWord = word;
                    while (GetRealLength(remainingWord, baseSize) > maxWidth)
                    {
                        // Find how many chars fit
                        int charsThatFit = 0;
                        int currentMeasured = 0;
                        for (int i = 1; i <= remainingWord.Length; i++)
                        {
                            int len = GetRealLength(remainingWord.Substring(0, i), baseSize);
                            if (len > maxWidth) break;
                            charsThatFit = i;
                            currentMeasured = len;
                        }
                        
                        if (charsThatFit == 0) charsThatFit = 1; // force at least one
                        
                        result.Add(remainingWord.Substring(0, charsThatFit));
                        remainingWord = remainingWord.Substring(charsThatFit);
                    }
                    currentLine = remainingWord;
                    currentLineLen = GetRealLength(currentLine, baseSize);
                }
                else
                {
                    currentLine = word;
                    currentLineLen = wordLen;
                }
            }
        }

        if (!string.IsNullOrEmpty(currentLine))
            result.Add(currentLine);

        return result;
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
        
        // If size is Large or ExtraLarge, spaces also double their width.
        // We must divide the padding by 2 to prevent shifting columns too much.
        int multiplier = (size == PrinterFontSize.Large || size == PrinterFontSize.ExtraLarge) ? 2 : 1;
        int spaceCount = diff / multiplier;
        int spaceSecond = (diff - (spaceCount * multiplier)) / multiplier; // handle rounding if odd width

        return a switch
        {
            TicketAlignment.Center => new string(' ', spaceCount / 2) + s + new string(' ', (spaceCount - (spaceCount / 2))),
            TicketAlignment.Right  => new string(' ', spaceCount) + s,
            _                      => s + new string(' ', spaceCount),
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

            // Aliases for common variables
            if (key == "DATE") key = "!date";
            if (key == "TIME") key = "!time";
            if (key == "DATETIME") key = "!datetime";

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
        "small"       => PrinterFontSize.Small,
        "medium"      => PrinterFontSize.Medium,
        "large"       => PrinterFontSize.Large,
        "extra-large" => PrinterFontSize.ExtraLarge,
        _             => PrinterFontSize.Normal
    };
}
