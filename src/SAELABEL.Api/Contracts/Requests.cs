namespace SAELABEL.Api.Contracts;

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
