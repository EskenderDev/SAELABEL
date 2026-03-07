using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace SAE.STUDIO.Core.Labels.Modelos
{
    public class SaeLabelsTemplate
    {
        [JsonPropertyName("brand")]
        public string Brand { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("part")]
        public string Part { get; set; } = string.Empty;

        [JsonPropertyName("size")]
        public string Size { get; set; } = string.Empty;

        [JsonPropertyName("productUrl")]
        public string ProductUrl { get; set; } = string.Empty;

        [JsonPropertyName("labelRectangle")]
        public LabelRectangle LabelRectangle { get; set; } = new();

        [JsonPropertyName("objects")]
        public List<TemplateObject> Objects { get; set; } = new();

        [JsonPropertyName("variables")]
        public List<TemplateVariable> Variables { get; set; } = new();

        [JsonPropertyName("lastModified")]
        public DateTime LastModified { get; set; } = DateTime.Now;

        [JsonPropertyName("filePath")]
        public string FilePath { get; set; } = string.Empty;

        [JsonIgnore]
        public Dictionary<string, IncrementalState> IncrementalStates { get; set; } = new();
    }

    public class LabelRectangle
    {
        [JsonPropertyName("width")]
        public double Width { get; set; }

        [JsonPropertyName("height")]
        public double Height { get; set; }

        [JsonPropertyName("round")]
        public double Round { get; set; }

        [JsonPropertyName("xWaste")]
        public double XWaste { get; set; }

        [JsonPropertyName("yWaste")]
        public double YWaste { get; set; }

        [JsonPropertyName("layout")]
        public Layout Layout { get; set; } = new();
    }

    public class Layout
    {
        [JsonPropertyName("dx")]
        public double Dx { get; set; }

        [JsonPropertyName("dy")]
        public double Dy { get; set; }

        [JsonPropertyName("nx")]
        public int Nx { get; set; }

        [JsonPropertyName("ny")]
        public int Ny { get; set; }

        [JsonPropertyName("x0")]
        public double X0 { get; set; }

        [JsonPropertyName("y0")]
        public double Y0 { get; set; }
    }

    public class TransformationMatrix
    {
        [JsonPropertyName("a")]
        public double A { get; set; } = 1;

        [JsonPropertyName("b")]
        public double B { get; set; } = 0;

        [JsonPropertyName("c")]
        public double C { get; set; } = 0;

        [JsonPropertyName("d")]
        public double D { get; set; } = 1;

        [JsonPropertyName("e")]
        public double E { get; set; } = 0;

        [JsonPropertyName("f")]
        public double F { get; set; } = 0;

        [JsonIgnore]
        public bool IsIdentity => A == 1 && B == 0 && C == 0 && D == 1 && E == 0 && F == 0;

        public TransformationMatrix() { }

        public TransformationMatrix(double a, double b, double c, double d, double e, double f)
        {
            A = a; B = b; C = c; D = d; E = e; F = f;
        }

        public static TransformationMatrix CreateRotationMatrix(double angleInDegrees)
        {
            var r = angleInDegrees * Math.PI / 180.0;
            var cos = Math.Cos(r);
            var sin = Math.Sin(r);
            return new TransformationMatrix(cos, -sin, sin, cos, 0, 0);
        }

        public static TransformationMatrix CreateScaleMatrix(double sx, double sy) => new(sx, 0, 0, sy, 0, 0);
        public static TransformationMatrix CreateTranslationMatrix(double tx, double ty) => new(1, 0, 0, 1, tx, ty);
    }

    public class ShadowEffect
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }
        [JsonPropertyName("color")]
        public string Color { get; set; } = "#000000";
        [JsonPropertyName("opacity")]
        public double Opacity { get; set; }
        [JsonPropertyName("offsetX")]
        public double OffsetX { get; set; }
        [JsonPropertyName("offsetY")]
        public double OffsetY { get; set; }
    }

    public class TemplateVariable
    {
        [JsonPropertyName("name")]
        [XmlAttribute("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        [XmlAttribute("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("initialValue")]
        [XmlAttribute("initialValue")]
        public string InitialValue { get; set; } = string.Empty;

        [JsonPropertyName("increment")]
        [XmlAttribute("increment")]
        public string Increment { get; set; } = "never";

        [JsonPropertyName("stepSize")]
        [XmlAttribute("stepSize")]
        public double StepSize { get; set; }

        [JsonIgnore]
        [XmlIgnore]
        public double CurrentValue { get; set; }

        [JsonIgnore]
        [XmlIgnore]
        public bool IsIncremental => Increment != "never" && StepSize != 0;

        public void Initialize()
        {
            if (double.TryParse(InitialValue, out double initial)) CurrentValue = initial;
            else CurrentValue = 0;
        }

        public void IncrementStep() => CurrentValue += StepSize;
        public void Reset() => Initialize();
    }

    public class IncrementalState
    {
        [JsonPropertyName("variableName")]
        public string VariableName { get; set; } = string.Empty;
        [JsonPropertyName("lastValue")]
        public int LastValue { get; set; }
        [JsonPropertyName("lastUsed")]
        public DateTime LastUsed { get; set; }
        [JsonPropertyName("totalIncrements")]
        public int TotalIncrements { get; set; }
        public void Update(int val) { LastValue = val; LastUsed = DateTime.Now; TotalIncrements++; }
    }

    public abstract class TemplateObject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public bool LockAspectRatio { get; set; }
        public TransformationMatrix Matrix { get; set; } = new();
        public ShadowEffect? Shadow { get; set; }
        public bool Rotate { get; set; }
        public float RotationAngle { get; set; }
    }

    public class TextObject : TemplateObject
    {
        public string Content { get; set; } = string.Empty;
        public string FontFamily { get; set; } = "Arial";
        public double FontSize { get; set; } = 10;
        public string Color { get; set; } = "#000000";
        public TextAlignment Alignment { get; set; }
        public VerticalAlignment VerticalAlignment { get; set; }
        public bool FontItalic { get; set; }
        public bool FontUnderline { get; set; }
        public string FontWeight { get; set; } = "normal";
        public double LineSpacing { get; set; } = 1.0;
        public bool AutoShrink { get; set; }
        public WrapMode WrapMode { get; set; }
    }

    public class BarcodeObject : TemplateObject
    {
        public string Data { get; set; } = string.Empty;
        public string BarcodeType { get; set; } = string.Empty;
        public bool ShowText { get; set; }
        public bool Checksum { get; set; }
        public string Color { get; set; } = "#000000";
        public string Backend { get; set; } = "gnu-barcode";
    }

    public class BoxObject : TemplateObject
    {
        public string FillColor { get; set; } = "none";
        public string LineColor { get; set; } = "#000000";
        public double LineWidth { get; set; } = 1.0;
    }

    public class LineObject : TemplateObject
    {
        public double Dx { get; set; }
        public double Dy { get; set; }
        public string LineColor { get; set; } = "#000000";
        public double LineWidth { get; set; } = 1.0;
    }

    public class EllipseObject : TemplateObject
    {
        public string FillColor { get; set; } = "none";
        public string LineColor { get; set; } = "#000000";
        public double LineWidth { get; set; } = 1.0;
    }

    public class ImageObject : TemplateObject
    {
        public string Source { get; set; } = string.Empty;
        public new bool LockAspectRatio { get; set; } = true;
    }

    public class PathObject : TemplateObject
    {
        public string Data { get; set; } = string.Empty;
        public string FillColor { get; set; } = "none";
        public string LineColor { get; set; } = "#000000";
        public double LineWidth { get; set; } = 1.0;
    }

    public enum TextAlignment { Left, Center, Right }
    public enum VerticalAlignment { Top, Middle, Bottom }
    public enum WrapMode { Word, Character, None }

    public class TemplateMapping
    {
        public string TemplateName { get; set; } = string.Empty;
        public Dictionary<string, VariableSource> Mappings { get; set; } = new Dictionary<string, VariableSource>();
    }

    public class VariableSource
    {
        public string Source { get; set; } = "Custom"; // Producto, Ubicacion, Custom
        public string Field { get; set; } = string.Empty; // Nombre, Precio, etc.
        public string? ConstantValue { get; set; }
    }
}
