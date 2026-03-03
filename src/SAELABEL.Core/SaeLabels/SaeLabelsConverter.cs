using SAELABEL.Core.Labels.Modelos;

namespace SAELABEL.Core.SaeLabels;

public static class SaeLabelsConverter
{
    public static SaeLabelsDocument FromGlabelsTemplate(GlabelsTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        var doc = new SaeLabelsDocument
        {
            Version = "1.0",
            Template = new SaeTemplate
            {
                Brand = template.Brand,
                Description = template.Description,
                Part = template.Part,
                Size = template.Size,
                ProductUrl = template.ProductUrl,
                WidthPt = template.LabelRectangle.Width,
                HeightPt = template.LabelRectangle.Height,
                RoundPt = template.LabelRectangle.Round,
                XWastePt = template.LabelRectangle.XWaste,
                YWastePt = template.LabelRectangle.YWaste,
                Layout = new SaeLayout
                {
                    DxPt = template.LabelRectangle.Layout.Dx,
                    DyPt = template.LabelRectangle.Layout.Dy,
                    Nx = template.LabelRectangle.Layout.Nx,
                    Ny = template.LabelRectangle.Layout.Ny,
                    X0Pt = template.LabelRectangle.Layout.X0,
                    Y0Pt = template.LabelRectangle.Layout.Y0
                }
            }
        };

        foreach (var v in template.Variables)
        {
            doc.Variables.Add(new SaeLabelVariable
            {
                Name = v.Name,
                Type = v.Type,
                InitialValue = v.InitialValue,
                Increment = v.Increment,
                StepSize = v.StepSize
            });
        }

        foreach (var obj in template.Objects)
        {
            doc.Objects.Add(obj switch
            {
                TextObject t => new SaeLabelObject
                {
                    Type = "text",
                    XPt = t.X,
                    YPt = t.Y,
                    WidthPt = t.Width,
                    HeightPt = t.Height,
                    Content = t.Content,
                    Style = t.FontFamily,
                    Color = t.Color
                },
                BarcodeObject b => new SaeLabelObject
                {
                    Type = "barcode",
                    XPt = b.X,
                    YPt = b.Y,
                    WidthPt = b.Width,
                    HeightPt = b.Height,
                    Content = b.Data,
                    Style = b.BarcodeType,
                    Color = b.Color,
                    ShowText = b.ShowText,
                    Checksum = b.Checksum
                },
                BoxObject b => new SaeLabelObject
                {
                    Type = "box",
                    XPt = b.X,
                    YPt = b.Y,
                    WidthPt = b.Width,
                    HeightPt = b.Height,
                    Color = b.LineColor
                },
                EllipseObject e => new SaeLabelObject
                {
                    Type = "ellipse",
                    XPt = e.X,
                    YPt = e.Y,
                    WidthPt = e.Width,
                    HeightPt = e.Height,
                    Color = e.LineColor
                },
                LineObject l => new SaeLabelObject
                {
                    Type = "line",
                    XPt = l.X,
                    YPt = l.Y,
                    WidthPt = l.Width,
                    HeightPt = l.Height,
                    DxPt = l.Dx,
                    DyPt = l.Dy,
                    Color = l.LineColor
                },
                ImageObject i => new SaeLabelObject
                {
                    Type = "image",
                    XPt = i.X,
                    YPt = i.Y,
                    WidthPt = i.Width,
                    HeightPt = i.Height,
                    Content = i.Source
                },
                _ => new SaeLabelObject { Type = "unknown" }
            });
        }

        return doc;
    }

    public static GlabelsTemplate ToGlabelsTemplate(SaeLabelsDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var template = new GlabelsTemplate
        {
            Brand = document.Template.Brand,
            Description = document.Template.Description,
            Part = document.Template.Part,
            Size = document.Template.Size,
            ProductUrl = document.Template.ProductUrl,
            LabelRectangle = new LabelRectangle
            {
                Width = document.Template.WidthPt,
                Height = document.Template.HeightPt,
                Round = document.Template.RoundPt,
                XWaste = document.Template.XWastePt,
                YWaste = document.Template.YWastePt,
                Layout = new Layout
                {
                    Dx = document.Template.Layout.DxPt,
                    Dy = document.Template.Layout.DyPt,
                    Nx = document.Template.Layout.Nx,
                    Ny = document.Template.Layout.Ny,
                    X0 = document.Template.Layout.X0Pt,
                    Y0 = document.Template.Layout.Y0Pt
                }
            }
        };

        foreach (var variable in document.Variables)
        {
            template.Variables.Add(new TemplateVariable
            {
                Name = variable.Name,
                Type = variable.Type,
                InitialValue = variable.InitialValue,
                Increment = variable.Increment,
                StepSize = variable.StepSize
            });
        }

        foreach (var obj in document.Objects)
        {
            switch (obj.Type.ToLowerInvariant())
            {
                case "text":
                    template.Objects.Add(new TextObject
                    {
                        X = obj.XPt,
                        Y = obj.YPt,
                        Width = obj.WidthPt,
                        Height = obj.HeightPt,
                        Content = obj.Content,
                        FontFamily = string.IsNullOrWhiteSpace(obj.Style) ? "Sans" : obj.Style,
                        Color = obj.Color
                    });
                    break;
                case "barcode":
                    template.Objects.Add(new BarcodeObject
                    {
                        X = obj.XPt,
                        Y = obj.YPt,
                        Width = obj.WidthPt,
                        Height = obj.HeightPt,
                        Data = obj.Content,
                        BarcodeType = string.IsNullOrWhiteSpace(obj.Style) ? "code39" : obj.Style,
                        Color = obj.Color,
                        ShowText = obj.ShowText,
                        Checksum = obj.Checksum
                    });
                    break;
                case "line":
                    template.Objects.Add(new LineObject
                    {
                        X = obj.XPt,
                        Y = obj.YPt,
                        Width = obj.WidthPt,
                        Height = obj.HeightPt,
                        Dx = obj.DxPt,
                        Dy = obj.DyPt,
                        LineColor = obj.Color
                    });
                    break;
                case "box":
                    template.Objects.Add(new BoxObject
                    {
                        X = obj.XPt,
                        Y = obj.YPt,
                        Width = obj.WidthPt,
                        Height = obj.HeightPt,
                        LineColor = obj.Color
                    });
                    break;
                case "ellipse":
                    template.Objects.Add(new EllipseObject
                    {
                        X = obj.XPt,
                        Y = obj.YPt,
                        Width = obj.WidthPt,
                        Height = obj.HeightPt,
                        LineColor = obj.Color
                    });
                    break;
                case "image":
                    template.Objects.Add(new ImageObject
                    {
                        X = obj.XPt,
                        Y = obj.YPt,
                        Width = obj.WidthPt,
                        Height = obj.HeightPt,
                        Source = obj.Content
                    });
                    break;
            }
        }

        return template;
    }
}
