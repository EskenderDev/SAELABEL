namespace SAELABEL.Api.Contracts;

public sealed class EditorElementDto
{
    public string Id { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "basic";
    public string ObjectType { get; set; } = "text";
    public double DefaultWidthPt { get; set; } = 80;
    public double DefaultHeightPt { get; set; } = 24;
    public string DefaultContent { get; set; } = string.Empty;
}

public sealed class UpsertEditorElementRequest
{
    public string? Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "basic";
    public string ObjectType { get; set; } = "text";
    public double DefaultWidthPt { get; set; } = 80;
    public double DefaultHeightPt { get; set; } = 24;
    public string DefaultContent { get; set; } = string.Empty;
}

public sealed class EditorDocumentSummaryDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = "sae";
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class EditorDocumentDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = "sae";
    public string Xml { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class UpsertEditorDocumentRequest
{
    public string? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = "sae";
    public string Xml { get; set; } = string.Empty;
}

