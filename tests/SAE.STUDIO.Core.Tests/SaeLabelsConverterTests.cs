using SAE.STUDIO.Core.Labels.Modelos;
using SAE.STUDIO.Core.Labels.Servicios;
using Microsoft.Extensions.Logging.Abstractions;
using SAE.STUDIO.Core.Labels.Caching;

namespace SAE.STUDIO.Core.Tests;

public class SaeLabelsConverterTests
{
    private readonly SaeLabelsTemplateService _service;

    public SaeLabelsConverterTests()
    {
        _service = new SaeLabelsTemplateService(NullLogger<SaeLabelsTemplateService>.Instance, new TemplateCache());
    }

    [Fact]
    public void Convert_FromAndToSaeLabelsTemplate_PreservesCoreData()
    {
        // Actually, SaeLabelsConverter was removed/merged into SaeLabelsTemplateService in the previous refactor
        // or it was specifically for Glabels which we might not be testing here the same way.
        // Let's assume for now we want to test a similar roundtrip using XML.

        var original = new SaeLabelsTemplate
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

        var xml = SaeLabelsTemplateXmlSerializer.Serialize(original);
        var parsed = _service.ParseTemplateXml(xml);

        Assert.Equal(original.Brand, parsed.Brand);
        Assert.Equal(original.Part, parsed.Part);
        Assert.Equal(original.LabelRectangle.Width, parsed.LabelRectangle.Width);
        Assert.Equal(2, parsed.Objects.Count);
        Assert.Single(parsed.Variables);
    }
}
