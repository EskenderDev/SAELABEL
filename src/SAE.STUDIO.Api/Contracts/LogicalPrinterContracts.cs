using System.Text.Json.Serialization;

namespace SAE.STUDIO.Api.Contracts;

public class PhysicalPrinterConfig
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("copies")]
    public int? Copies { get; set; }
    
    [JsonPropertyName("paperWidth")]
    public int? PaperWidth { get; set; }

    [JsonPropertyName("paperHeight")]
    public int? PaperHeight { get; set; }
}

public class LogicalPrinterDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    
    [JsonPropertyName("printers")]
    public List<PhysicalPrinterConfig> Printers { get; set; } = new();
    
    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;
    
    [JsonPropertyName("copies")]
    public int Copies { get; set; } = 1;
    
    [JsonPropertyName("paperWidth")]
    public int? PaperWidth { get; set; }

    [JsonPropertyName("paperHeight")]
    public int? PaperHeight { get; set; }
    
    [JsonPropertyName("mediaType")]
    public string MediaType { get; set; } = "receipt";
}

public class UpsertLogicalPrinterRequest
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    
    [JsonPropertyName("printers")]
    public List<PhysicalPrinterConfig> Printers { get; set; } = new();
    
    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;
    
    [JsonPropertyName("copies")]
    public int Copies { get; set; } = 1;
    
    [JsonPropertyName("paperWidth")]
    public int? PaperWidth { get; set; }

    [JsonPropertyName("paperHeight")]
    public int? PaperHeight { get; set; }
    
    [JsonPropertyName("mediaType")]
    public string MediaType { get; set; } = "receipt";
}
