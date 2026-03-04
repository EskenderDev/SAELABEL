namespace SAELABEL.Api.Contracts;

public class LogicalPrinterDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string PhysicalPrinter { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    /// <summary>Número de copias por impresión (default 1).</summary>
    public int Copies { get; set; } = 1;
    /// <summary>Ancho de papel en mm: 58 o 80.</summary>
    public int PaperWidth { get; set; } = 80;
    /// <summary>Tipo de medio: "receipt" | "label". Ayuda al servidor local a filtrar.</summary>
    public string MediaType { get; set; } = "receipt";
}

public class UpsertLogicalPrinterRequest
{
    public string? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string PhysicalPrinter { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int Copies { get; set; } = 1;
    public int PaperWidth { get; set; } = 80;
    public string MediaType { get; set; } = "receipt";
}
