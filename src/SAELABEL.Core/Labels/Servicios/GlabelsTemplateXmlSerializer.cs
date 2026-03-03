using SAELABEL.Core.Labels.Modelos;
using SAELABEL.Core.Labels.Helpers;
using System.Globalization;
using System.Xml.Linq;

namespace SAELABEL.Core.Labels.Servicios;

public static class GlabelsTemplateXmlSerializer
{
    public static string Serialize(GlabelsTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        var root = new XElement("Glabels-document");
        var templateNode = new XElement("Template",
            new XAttribute("brand", template.Brand),
            new XAttribute("description", template.Description),
            new XAttribute("part", template.Part),
            new XAttribute("size", template.Size)
        );

        if (!string.IsNullOrWhiteSpace(template.ProductUrl))
        {
            templateNode.Add(new XElement("Meta", new XAttribute("product_url", template.ProductUrl)));
        }

        templateNode.Add(new XElement("Label-rectangle",
            new XAttribute("width", Pt(template.LabelRectangle.Width)),
            new XAttribute("height", Pt(template.LabelRectangle.Height)),
            new XAttribute("round", Pt(template.LabelRectangle.Round)),
            new XAttribute("x_waste", Pt(template.LabelRectangle.XWaste)),
            new XAttribute("y_waste", Pt(template.LabelRectangle.YWaste)),
            new XElement("Layout",
                new XAttribute("dx", Pt(template.LabelRectangle.Layout.Dx)),
                new XAttribute("dy", Pt(template.LabelRectangle.Layout.Dy)),
                new XAttribute("nx", template.LabelRectangle.Layout.Nx),
                new XAttribute("ny", template.LabelRectangle.Layout.Ny),
                new XAttribute("x0", Pt(template.LabelRectangle.Layout.X0)),
                new XAttribute("y0", Pt(template.LabelRectangle.Layout.Y0))
            )));

        root.Add(templateNode);

        var objectsNode = new XElement("Objects");
        foreach (var obj in template.Objects)
        {
            objectsNode.Add(obj switch
            {
                TextObject t => new XElement("Object-text",
                    Common(obj),
                    new XAttribute("font_family", t.FontFamily),
                    new XAttribute("font_size", t.FontSize.ToString("0.####", CultureInfo.InvariantCulture)),
                    new XAttribute("color", t.Color),
                    new XElement("p", t.Content)),
                BarcodeObject b => new XElement("Object-barcode",
                    Common(obj),
                    new XAttribute("data", b.Data),
                    new XAttribute("style", b.BarcodeType),
                    new XAttribute("text", b.ShowText),
                    new XAttribute("checksum", b.Checksum),
                    new XAttribute("color", b.Color)),
                BoxObject b => new XElement("Object-box",
                    Common(obj),
                    new XAttribute("line_color", b.LineColor),
                    new XAttribute("line_width", Pt(b.LineWidth)),
                    new XAttribute("fill_color", b.FillColor)),
                EllipseObject e => new XElement("Object-ellipse",
                    Common(obj),
                    new XAttribute("line_color", e.LineColor),
                    new XAttribute("line_width", Pt(e.LineWidth)),
                    new XAttribute("fill_color", e.FillColor)),
                LineObject l => new XElement("Object-line",
                    Common(obj),
                    new XAttribute("dx", Pt(l.Dx)),
                    new XAttribute("dy", Pt(l.Dy)),
                    new XAttribute("line_color", l.LineColor),
                    new XAttribute("line_width", Pt(l.LineWidth))),
                ImageObject i => new XElement("Object-image",
                    Common(obj),
                    new XAttribute("src", i.Source),
                    new XAttribute("lock_aspect_ratio", i.LockAspectRatio)),
                _ => null
            });
        }

        root.Add(objectsNode);

        if (template.Variables.Count > 0)
        {
            var varsNode = new XElement("Variables");
            foreach (var v in template.Variables)
            {
                var normalizedType = VariableTypeNormalizer.Normalize(v.Type);
                var variableNode = new XElement("Variable",
                    new XAttribute("name", v.Name),
                    new XAttribute("type", ToGlabelsTypeId(normalizedType)),
                    new XAttribute("initialValue", v.InitialValue));

                if (normalizedType == VariableTypeNormalizer.Integer || normalizedType == VariableTypeNormalizer.FloatingPoint)
                {
                    var increment = IncrementModeNormalizer.Normalize(v.Increment);
                    variableNode.Add(new XAttribute("increment", increment));
                    if (increment != IncrementModeNormalizer.None)
                    {
                        variableNode.Add(new XAttribute("stepSize", v.StepSize.ToString("0.###############", CultureInfo.InvariantCulture)));
                    }
                }

                varsNode.Add(variableNode);
            }

            root.Add(varsNode);
        }

        return new XDocument(root).ToString();
    }

    private static object[] Common(TemplateObject o) =>
    [
        new XAttribute("x", Pt(o.X)),
        new XAttribute("y", Pt(o.Y)),
        new XAttribute("w", Pt(o.Width)),
        new XAttribute("h", Pt(o.Height)),
        new XAttribute("lock_aspect_ratio", o.LockAspectRatio),
        new XAttribute("a0", o.Matrix.A.ToString("0.####", CultureInfo.InvariantCulture)),
        new XAttribute("a1", o.Matrix.B.ToString("0.####", CultureInfo.InvariantCulture)),
        new XAttribute("a2", o.Matrix.C.ToString("0.####", CultureInfo.InvariantCulture)),
        new XAttribute("a3", o.Matrix.D.ToString("0.####", CultureInfo.InvariantCulture)),
        new XAttribute("a4", o.Matrix.E.ToString("0.####", CultureInfo.InvariantCulture)),
        new XAttribute("a5", o.Matrix.F.ToString("0.####", CultureInfo.InvariantCulture))
    ];

    private static string Pt(double v) => $"{v.ToString("0.####", CultureInfo.InvariantCulture)}pt";

    private static string ToGlabelsTypeId(string normalizedType) =>
        normalizedType == VariableTypeNormalizer.FloatingPoint ? "float" : normalizedType;
}
