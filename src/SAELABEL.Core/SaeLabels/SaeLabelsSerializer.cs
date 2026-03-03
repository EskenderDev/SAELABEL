using System.Globalization;
using System.Xml.Linq;

namespace SAELABEL.Core.SaeLabels;

public static class SaeLabelsSerializer
{
    public static string Serialize(SaeLabelsDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var root = new XElement("saelabels",
            new XAttribute("version", document.Version),
            new XElement("template",
                new XAttribute("brand", document.Template.Brand),
                new XAttribute("description", document.Template.Description),
                new XAttribute("part", document.Template.Part),
                new XAttribute("size", document.Template.Size),
                new XAttribute("product_url", document.Template.ProductUrl),
                new XElement("label_rectangle",
                    new XAttribute("width_pt", F(document.Template.WidthPt)),
                    new XAttribute("height_pt", F(document.Template.HeightPt)),
                    new XAttribute("round_pt", F(document.Template.RoundPt)),
                    new XAttribute("x_waste_pt", F(document.Template.XWastePt)),
                    new XAttribute("y_waste_pt", F(document.Template.YWastePt))),
                new XElement("layout",
                    new XAttribute("dx_pt", F(document.Template.Layout.DxPt)),
                    new XAttribute("dy_pt", F(document.Template.Layout.DyPt)),
                    new XAttribute("nx", document.Template.Layout.Nx),
                    new XAttribute("ny", document.Template.Layout.Ny),
                    new XAttribute("x0_pt", F(document.Template.Layout.X0Pt)),
                    new XAttribute("y0_pt", F(document.Template.Layout.Y0Pt))
                )
            ),
            new XElement("objects", document.Objects.Select(WriteObject)),
            new XElement("variables", document.Variables.Select(v =>
                new XElement("variable",
                    new XAttribute("name", v.Name),
                    new XAttribute("type", v.Type),
                    new XAttribute("initial", v.InitialValue),
                    new XAttribute("increment", v.Increment),
                    new XAttribute("step", v.StepSize)
                )))
        );

        return new XDocument(root).ToString();
    }

    public static SaeLabelsDocument Deserialize(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            throw new ArgumentException("XML vacío.", nameof(xml));
        }

        var doc = XDocument.Parse(xml);
        var root = doc.Root ?? throw new InvalidDataException("XML sin nodo raíz.");
        if (root.Name.LocalName != "saelabels")
        {
            throw new InvalidDataException("Documento no es saelabels.");
        }

        var templateNode = root.Element("template") ?? throw new InvalidDataException("Falta nodo template.");
        var rectNode = templateNode.Element("label_rectangle") ?? throw new InvalidDataException("Falta label_rectangle.");
        var layoutNode = templateNode.Element("layout") ?? throw new InvalidDataException("Falta layout.");

        var result = new SaeLabelsDocument
        {
            Version = A(root, "version", "1.0"),
            Template = new SaeTemplate
            {
                Brand = A(templateNode, "brand"),
                Description = A(templateNode, "description"),
                Part = A(templateNode, "part"),
                Size = A(templateNode, "size"),
                ProductUrl = A(templateNode, "product_url"),
                WidthPt = D(rectNode, "width_pt"),
                HeightPt = D(rectNode, "height_pt"),
                RoundPt = D(rectNode, "round_pt"),
                XWastePt = D(rectNode, "x_waste_pt"),
                YWastePt = D(rectNode, "y_waste_pt"),
                Layout = new SaeLayout
                {
                    DxPt = D(layoutNode, "dx_pt"),
                    DyPt = D(layoutNode, "dy_pt"),
                    Nx = I(layoutNode, "nx", 1),
                    Ny = I(layoutNode, "ny", 1),
                    X0Pt = D(layoutNode, "x0_pt"),
                    Y0Pt = D(layoutNode, "y0_pt")
                }
            }
        };

        var objectsNode = root.Element("objects");
        if (objectsNode is not null)
        {
            foreach (var o in objectsNode.Elements("object"))
            {
                result.Objects.Add(new SaeLabelObject
                {
                    Type = A(o, "type"),
                    XPt = D(o, "x_pt"),
                    YPt = D(o, "y_pt"),
                    WidthPt = D(o, "w_pt"),
                    HeightPt = D(o, "h_pt"),
                    Content = (o.Element("content")?.Value ?? string.Empty).Trim(),
                    Style = A(o, "style"),
                    Color = A(o, "color"),
                    DxPt = D(o, "dx_pt"),
                    DyPt = D(o, "dy_pt"),
                    ShowText = B(o, "show_text", false),
                    Checksum = B(o, "checksum", false)
                });
            }
        }

        var variablesNode = root.Element("variables");
        if (variablesNode is not null)
        {
            foreach (var v in variablesNode.Elements("variable"))
            {
                result.Variables.Add(new SaeLabelVariable
                {
                    Name = A(v, "name"),
                    Type = A(v, "type", "integer"),
                    InitialValue = A(v, "initial", "0"),
                    Increment = A(v, "increment", "never"),
                    StepSize = I(v, "step", 0)
                });
            }
        }

        return result;
    }

    private static XElement WriteObject(SaeLabelObject o) =>
        new("object",
            new XAttribute("type", o.Type),
            new XAttribute("x_pt", F(o.XPt)),
            new XAttribute("y_pt", F(o.YPt)),
            new XAttribute("w_pt", F(o.WidthPt)),
            new XAttribute("h_pt", F(o.HeightPt)),
            new XAttribute("style", o.Style),
            new XAttribute("color", o.Color),
            new XAttribute("dx_pt", F(o.DxPt)),
            new XAttribute("dy_pt", F(o.DyPt)),
            new XAttribute("show_text", o.ShowText),
            new XAttribute("checksum", o.Checksum),
            new XElement("content", o.Content));

    private static string F(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
    private static string A(XElement node, string name, string fallback = "") => node.Attribute(name)?.Value ?? fallback;

    private static double D(XElement node, string name)
        => double.TryParse(A(node, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 0.0;

    private static int I(XElement node, string name, int fallback)
        => int.TryParse(A(node, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : fallback;

    private static bool B(XElement node, string name, bool fallback)
        => bool.TryParse(A(node, name), out var b) ? b : fallback;
}
