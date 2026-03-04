namespace SAE.STUDIO.Api.Contracts;

public sealed class XmlPayload
{
    public string Xml { get; set; } = string.Empty;
}

public sealed class RenderRequest
{
    public string Xml { get; set; } = string.Empty;
    public string Format { get; set; } = "png";
    public Dictionary<string, string>? Data { get; set; }
}

public sealed class ExportRequest
{
    public string Xml { get; set; } = string.Empty;
    public string? FileName { get; set; }
}

public sealed class PrintRequest
{
    public string Xml { get; set; } = string.Empty;
    public string PrinterName { get; set; } = string.Empty;
    public int Copies { get; set; } = 1;
    public Dictionary<string, string>? Data { get; set; }
    public List<Dictionary<string, string>>? DataList { get; set; }
}

public sealed class ZplRequest
{
    public string Xml { get; set; } = string.Empty;
    public int Copies { get; set; } = 1;
    public Dictionary<string, string>? Data { get; set; }
}
