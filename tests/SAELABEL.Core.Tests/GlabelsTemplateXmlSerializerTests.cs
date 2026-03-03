using SAELABEL.Core.Labels.Modelos;
using SAELABEL.Core.Labels.Servicios;

namespace SAELABEL.Core.Tests;

public class GlabelsTemplateXmlSerializerTests
{
    [Fact]
    public void Serialize_ShouldGenerateParsableGlabelsXml()
    {
        var template = new GlabelsTemplate
        {
            Brand = "Avery",
            Description = "Test label",
            Part = "8160",
            Size = "US-Letter",
            LabelRectangle = new LabelRectangle
            {
                Width = 189,
                Height = 72,
                Layout = new Layout { Nx = 3, Ny = 10, Dx = 200, Dy = 72, X0 = 11.25, Y0 = 36 }
            },
            Objects = new List<TemplateObject>
            {
                new TextObject { X = 10, Y = 10, Width = 100, Height = 20, Content = "${SKU}", FontFamily = "Arial", Color = "000000FF" }
            },
            Variables = new List<TemplateVariable>
            {
                new() { Name = "SKU", Type = "string", InitialValue = "A-1", Increment = "never", StepSize = 0 }
            }
        };

        var xml = GlabelsTemplateXmlSerializer.Serialize(template);

        var parser = new GlabelsTemplateService(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<GlabelsTemplateService>.Instance,
            new SAELABEL.Core.Labels.Caching.TemplateCache());
        var parsed = parser.ParseTemplateXml(xml);

        Assert.Equal("Avery", parsed.Brand);
        Assert.Single(parsed.Objects);
        Assert.Single(parsed.Variables);
    }
}
