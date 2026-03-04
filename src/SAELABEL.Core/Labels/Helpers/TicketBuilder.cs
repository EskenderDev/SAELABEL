using System.Text;
using SAELABEL.Core.Labels.Modelos;

namespace SAELABEL.Core.Labels.Helpers;

public class TicketBuilder
{
    private readonly List<byte> _buffer = new();
    private readonly int _width;
    private readonly Encoding _encoding;

    // ESC/POS Commands
    private static readonly byte[] Initialize = { 27, 64 };
    private static readonly byte[] BoldOn = { 27, 69, 1 };
    private static readonly byte[] BoldOff = { 27, 69, 0 };
    private static readonly byte[] DoubleStrikeOn = { 27, 71, 1 };
    private static readonly byte[] DoubleStrikeOff = { 27, 71, 0 };
    private static readonly byte[] CutPaper   = { 29, 86, 65, 3 };
    private static readonly byte[] OpenDrawerCmd = { 27, 112, 0, 15, 150 };
    private static readonly byte[] BeepCmd       = { 27, 66, 2, 1 };
    private static readonly byte[] AlignLeft = { 27, 97, 0 };
    private static readonly byte[] AlignCenter = { 27, 97, 1 };
    private static readonly byte[] AlignRight = { 27, 97, 2 };

    public TicketBuilder(int width = 40, Encoding? encoding = null)
    {
        _width = width;
        _encoding = encoding ?? Encoding.UTF8;
        _buffer.AddRange(Initialize);
    }

    private void Append(string text) => _buffer.AddRange(_encoding.GetBytes(text));
    private void AppendLine(string text = "")
    {
        if (!string.IsNullOrEmpty(text)) Append(text);
        _buffer.Add(10); // LF
    }

    public TicketBuilder Separator(char c = '-')
    {
        AppendLine(new string(c, _width));
        return this;
    }

    public TicketBuilder SetAlignment(TicketAlignment alignment)
    {
        _buffer.AddRange(alignment switch
        {
            TicketAlignment.Center => AlignCenter,
            TicketAlignment.Right => AlignRight,
            _ => AlignLeft
        });
        return this;
    }

    public TicketBuilder NewLine()
    {
        _buffer.Add(10);
        return this;
    }

    public TicketBuilder TextPart(string text, bool bold = false, PrinterFontSize size = PrinterFontSize.Normal, bool extraBold = false)
    {
        if (size != PrinterFontSize.Normal) SetFontSize(size);
        if (bold) _buffer.AddRange(BoldOn);
        if (extraBold) _buffer.AddRange(DoubleStrikeOn);
        
        Append(text);

        if (extraBold) _buffer.AddRange(DoubleStrikeOff);
        if (bold) _buffer.AddRange(BoldOff);
        if (size != PrinterFontSize.Normal) SetFontSize(PrinterFontSize.Normal);
        
        return this;
    }

    public TicketBuilder Text(string text, TicketAlignment alignment = TicketAlignment.Left, bool bold = false, PrinterFontSize size = PrinterFontSize.Normal)
    {
        SetAlignment(alignment);
        
        var lines = WrapText(text, _width);
        foreach (var line in lines)
        {
            TextPart(line, bold, size);
            NewLine();
        }

        if (alignment != TicketAlignment.Left) SetAlignment(TicketAlignment.Left);
        
        return this;
    }

    public TicketBuilder Item(string description, string quantity, string? price = null, string? total = null)
    {
        // Simplificación para el parser XML
        string qty = quantity.PadRight(6);
        string totalTxt = !string.IsNullOrEmpty(total) ? total.PadLeft(10) : "";
        
        var availableWidth = _width - (!string.IsNullOrEmpty(price) ? 18 : 6);
        var lines = WrapText(description, availableWidth);
        
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            if (i == 0)
            {
                Append(qty);
                Append(line.PadRight(availableWidth));
                if (!string.IsNullOrEmpty(total)) Append(totalTxt);
                AppendLine();
            }
            else
            {
                Append(new string(' ', 6));
                AppendLine(line);
            }
        }
        
        return this;
    }

    public TicketBuilder SetFontSize(PrinterFontSize size)
    {
        _buffer.AddRange(new byte[] { 27, 33, (byte)size });
        return this;
    }

    public TicketBuilder Feed(int lines = 1)
    {
        for (int i = 0; i < lines; i++) _buffer.Add(10);
        return this;
    }

    public TicketBuilder Beep()
    {
        _buffer.AddRange(BeepCmd);
        return this;
    }

    public TicketBuilder Cut()
    {
        _buffer.AddRange(CutPaper);
        return this;
    }

    public TicketBuilder QrCode(string data, TicketAlignment alignment = TicketAlignment.Center, int moduleSize = 6)
    {
        byte[] dataBytes = _encoding.GetBytes(data);
        int storeLen = dataBytes.Length + 3;
        byte pL = (byte)(storeLen % 256);
        byte pH = (byte)(storeLen / 256);

        if (alignment == TicketAlignment.Center) _buffer.AddRange(AlignCenter);
        else if (alignment == TicketAlignment.Right) _buffer.AddRange(AlignRight);

        _buffer.AddRange(new byte[] { 29, 40, 107, 4, 0, 49, 65, 50, 0 });
        _buffer.AddRange(new byte[] { 29, 40, 107, 3, 0, 49, 67, (byte)Math.Max(1, Math.Min(16, moduleSize)) });
        _buffer.AddRange(new byte[] { 29, 40, 107, 3, 0, 49, 69, 49 });
        _buffer.AddRange(new byte[] { 29, 40, 107, pL, pH, 49, 80, 48 });
        _buffer.AddRange(dataBytes);
        _buffer.AddRange(new byte[] { 29, 40, 107, 3, 0, 49, 81, 48 });

        if (alignment != TicketAlignment.Left) _buffer.AddRange(AlignLeft);
        
        Feed(1);
        return this;
    }

    public TicketBuilder OpenDrawer()
    {
        _buffer.AddRange(OpenDrawerCmd);
        return this;
    }

    public byte[] Build() => _buffer.ToArray();

    private static List<string> WrapText(string text, int width)
    {
        if (string.IsNullOrEmpty(text)) return new List<string> { "" };
        var lines = new List<string>();
        for (int i = 0; i < text.Length; i += width)
        {
            lines.Add(text.Substring(i, Math.Min(width, text.Length - i)));
        }
        return lines;
    }
}
