namespace SAELABEL.Core.SaeLabels;

public sealed class SaeLabelsDocument
{
    public string Version { get; set; } = "1.0";
    public SaeTemplate Template { get; set; } = new();
    public List<SaeLabelObject> Objects { get; set; } = new();
    public List<SaeLabelVariable> Variables { get; set; } = new();
}

public sealed class SaeTemplate
{
    public string Brand { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Part { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string ProductUrl { get; set; } = string.Empty;
    public double WidthPt { get; set; }
    public double HeightPt { get; set; }
    public double RoundPt { get; set; }
    public double XWastePt { get; set; }
    public double YWastePt { get; set; }
    public SaeLayout Layout { get; set; } = new();
}

public sealed class SaeLayout
{
    public double DxPt { get; set; }
    public double DyPt { get; set; }
    public int Nx { get; set; } = 1;
    public int Ny { get; set; } = 1;
    public double X0Pt { get; set; }
    public double Y0Pt { get; set; }
}

public sealed class SaeLabelObject
{
    public string Type { get; set; } = string.Empty;
    public double XPt { get; set; }
    public double YPt { get; set; }
    public double WidthPt { get; set; }
    public double HeightPt { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Style { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public double DxPt { get; set; }
    public double DyPt { get; set; }
    public bool ShowText { get; set; }
    public bool Checksum { get; set; }
}

public sealed class SaeLabelVariable
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "integer";
    public string InitialValue { get; set; } = "0";
    public string Increment { get; set; } = "never";
    public double StepSize { get; set; }
}
