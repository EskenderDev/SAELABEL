using SAELABEL.Core.Labels.Modelos;
using SAELABEL.Core.SaeLabels;

namespace SAELABEL.Core.Tests;

public class SaeLabelsConverterTests
{
    [Fact]
    public void Convert_FromAndToGlabelsTemplate_PreservesCoreData()
    {
        var original = new GlabelsTemplate
        {
            Brand = "Avery",
            Description = "Mailing Labels",
            Part = "8160",
            Size = "US-Letter",
            LabelRectangle = new LabelRectangle
            {
                Width = 189,
                Height = 72,
                Layout = new Layout { Nx = 3, Ny = 10, Dx = 200, Dy = 72, X0 = 11.25, Y0 = 36 }
            },
            Variables = new List<TemplateVariable>
            {
                new() { Name = "SKU", Type = "string", InitialValue = "SKU-001", Increment = "never", StepSize = 0 }
            },
            Objects = new List<TemplateObject>
            {
                new TextObject { X = 10, Y = 10, Width = 120, Height = 20, Content = "${SKU}", FontFamily = "Arial", Color = "000000FF" },
                new BarcodeObject { X = 10, Y = 35, Width = 120, Height = 25, Data = "${SKU}", BarcodeType = "code128", ShowText = true }
            }
        };

        var sae = SaeLabelsConverter.FromGlabelsTemplate(original);
        var xml = SaeLabelsSerializer.Serialize(sae);
        var parsed = SaeLabelsSerializer.Deserialize(xml);
        var back = SaeLabelsConverter.ToGlabelsTemplate(parsed);

        Assert.Equal(original.Brand, back.Brand);
        Assert.Equal(original.Part, back.Part);
        Assert.Equal(original.LabelRectangle.Width, back.LabelRectangle.Width);
        Assert.Equal(2, back.Objects.Count);
        Assert.Single(back.Variables);
    }
}
