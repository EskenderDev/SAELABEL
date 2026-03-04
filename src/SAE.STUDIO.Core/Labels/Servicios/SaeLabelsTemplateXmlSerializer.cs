using SAE.STUDIO.Core.Labels.Modelos;
using SAE.STUDIO.Core.Labels.Helpers;
using System.Globalization;
using System.Xml.Linq;

namespace SAE.STUDIO.Core.Labels.Servicios;

public static class SaeLabelsTemplateXmlSerializer
{
    public static string Serialize(SaeLabelsTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        var root = new XElement("SaeLabels", new XAttribute("version", "1.0"));

        var templateNode = new XElement("template",
            new XAttribute("brand", template.Brand),
            new XAttribute("description", template.Description),
            new XAttribute("part", template.Part),
            new XAttribute("size", template.Size)
        );

        if (!string.IsNullOrWhiteSpace(template.ProductUrl))
        {
            templateNode.Add(new XAttribute("product_url", template.ProductUrl));
        }

        templateNode.Add(new XElement("label_rectangle",
            new XAttribute("width_pt", num(template.LabelRectangle.Width)),
            new XAttribute("height_pt", num(template.LabelRectangle.Height)),
            new XAttribute("round_pt", num(template.LabelRectangle.Round)),
            new XAttribute("x_waste_pt", num(template.LabelRectangle.XWaste)),
            new XAttribute("y_waste_pt", num(template.LabelRectangle.YWaste))
        ));

        templateNode.Add(new XElement("layout",
            new XAttribute("dx_pt", num(template.LabelRectangle.Layout.Dx)),
            new XAttribute("dy_pt", num(template.LabelRectangle.Layout.Dy)),
            new XAttribute("nx", template.LabelRectangle.Layout.Nx),
            new XAttribute("ny", template.LabelRectangle.Layout.Ny),
            new XAttribute("x0_pt", num(template.LabelRectangle.Layout.X0)),
            new XAttribute("y0_pt", num(template.LabelRectangle.Layout.Y0))
        ));

        root.Add(templateNode);

        var objectsNode = new XElement("objects");
        foreach (var obj in template.Objects)
        {
            var el = new XElement("object", Common(obj));

            switch (obj)
            {
                case TextObject t:
                    el.Add(new XAttribute("type", "text"));
                    el.Add(new XAttribute("style", t.FontFamily ?? ""));
                    el.Add(new XAttribute("color", t.Color ?? ""));
                    el.Add(new XElement("content", t.Content ?? ""));
                    break;
                case BarcodeObject b:
                    el.Add(new XAttribute("type", "barcode"));
                    el.Add(new XAttribute("style", b.BarcodeType ?? ""));
                    el.Add(new XAttribute("show_text", b.ShowText));
                    el.Add(new XAttribute("checksum", b.Checksum));
                    el.Add(new XAttribute("color", b.Color ?? ""));
                    el.Add(new XElement("content", b.Data ?? ""));
                    break;
                case BoxObject bx:
                    el.Add(new XAttribute("type", "box"));
                    el.Add(new XAttribute("line_color", bx.LineColor ?? ""));
                    el.Add(new XAttribute("line_width", num(bx.LineWidth)));
                    el.Add(new XAttribute("fill_color", bx.FillColor ?? ""));
                    break;
                case EllipseObject e:
                    el.Add(new XAttribute("type", "ellipse"));
                    el.Add(new XAttribute("line_color", e.LineColor ?? ""));
                    el.Add(new XAttribute("line_width", num(e.LineWidth)));
                    el.Add(new XAttribute("fill_color", e.FillColor ?? ""));
                    break;
                case LineObject l:
                    el.Add(new XAttribute("type", "line"));
                    el.Add(new XAttribute("dx_pt", num(l.Dx)));
                    el.Add(new XAttribute("dy_pt", num(l.Dy)));
                    el.Add(new XAttribute("line_color", l.LineColor ?? ""));
                    el.Add(new XAttribute("line_width", num(l.LineWidth)));
                    break;
                case PathObject p:
                    el.Add(new XAttribute("type", "path"));
                    el.Add(new XAttribute("line_color", p.LineColor ?? ""));
                    el.Add(new XAttribute("line_width", num(p.LineWidth)));
                    el.Add(new XAttribute("fill_color", p.FillColor ?? ""));
                    el.Add(new XElement("content", p.Data ?? ""));
                    break;
                case ImageObject i:
                    el.Add(new XAttribute("type", "image"));
                    el.Add(new XElement("content", i.Source ?? ""));
                    break;
            }

            // Note on transformations:
            // Since we extracted the rotation/scale in the Parser natively as properties,
            // we should serialize them out directly.
            // But if `SaeLabelsTemplate` doesn't hold `RotateDeg`, we rely on Matrix.
            // SaeLabels.xsd expects rot_deg, scale_x, etc or just uses matrix behind the scene?
            // Actually in SaeLabels we use rot_deg, scale_x, scale_y, skew_x, skew_y.
            // I'll emit rot_deg, scale_x, scale_y to simulate what SaeLabels format produces since the ui expects them.
            // The simplest approach is to add generic matrix extraction or trust the matrix entirely.
            // Let's just output the variables we know.
            
            objectsNode.Add(el);
        }

        root.Add(objectsNode);

        if (template.Variables.Count > 0)
        {
            var varsNode = new XElement("variables");
            foreach (var v in template.Variables)
            {
                var normalizedType = VariableTypeNormalizer.Normalize(v.Type);
                var variableNode = new XElement("variable",
                    new XAttribute("name", v.Name),
                    new XAttribute("type", ToSaeLabelsTypeId(normalizedType)),
                    new XAttribute("initial", v.InitialValue ?? ""));

                if (normalizedType == VariableTypeNormalizer.Integer || normalizedType == VariableTypeNormalizer.FloatingPoint)
                {
                    var increment = IncrementModeNormalizer.Normalize(v.Increment);
                    variableNode.Add(new XAttribute("increment", increment));
                    if (increment != IncrementModeNormalizer.None)
                    {
                        variableNode.Add(new XAttribute("step", num(v.StepSize)));
                    }
                }

                varsNode.Add(variableNode);
            }

            root.Add(varsNode);
        }

        return new XDocument(root).ToString();
    }

    private static object[] Common(TemplateObject o)
    {
        // Compute rot, scale, etc from matrix
        var a = o.Matrix.A;
        var b = o.Matrix.B;
        var c = o.Matrix.C;
        var d = o.Matrix.D;

        var scale_x = Math.Sqrt(a * a + b * b);
        var scale_y = Math.Sqrt(c * c + d * d);
        var rot_deg = Math.Atan2(b, a) * (180.0 / Math.PI);
        // Approximation
        
        return [
            new XAttribute("x_pt", num(o.X)),
            new XAttribute("y_pt", num(o.Y)),
            new XAttribute("w_pt", num(o.Width)),
            new XAttribute("h_pt", num(o.Height)),
            new XAttribute("rot_deg", num(rot_deg)),
            new XAttribute("scale_x", num(scale_x)),
            new XAttribute("scale_y", num(scale_y)),
            new XAttribute("skew_x", "0"),
            new XAttribute("skew_y", "0")
        ];
    }

    private static string num(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);

    private static string ToSaeLabelsTypeId(string normalizedType) => normalizedType;
}
