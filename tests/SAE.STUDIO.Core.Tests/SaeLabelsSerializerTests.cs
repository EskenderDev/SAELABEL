using SAE.STUDIO.Core.Labels.Servicios;
using SAE.STUDIO.Core.Labels.Modelos;
using Microsoft.Extensions.Logging.Abstractions;
using SAE.STUDIO.Core.Labels.Caching;

namespace SAE.STUDIO.Core.Tests;

public class SaeLabelsSerializerTests
{
    private readonly SaeLabelsTemplateService _service;

    public SaeLabelsSerializerTests()
    {
        _service = new SaeLabelsTemplateService(NullLogger<SaeLabelsTemplateService>.Instance, new TemplateCache());
    }

    [Fact]
    public void SerializeAndDeserialize_Roundtrip_PreservesMainFields()
    {
        var template = new SaeLabelsTemplate
        {
            Brand = "SAE",
            Description = "Etiqueta producto",
            Part = "P-100",
            Size = "custom",
            LabelRectangle = new LabelRectangle
            {
                Width = 144,
                Height = 72,
                Layout = new Layout { Nx = 1, Ny = 1 }
            },
            Objects = new List<TemplateObject>
            {
                new TextObject { Content = "${SKU}", X = 10, Y = 12, Width = 100, Height = 20, FontFamily = "Arial", Color = "000000FF" },
                new BarcodeObject { Data = "${SKU}", X = 10, Y = 36, Width = 120, Height = 30, BarcodeType = "code128", ShowText = true }
            },
            Variables = new List<TemplateVariable>
            {
                new() { Name = "SKU", Type = "string", InitialValue = "ABC-1", Increment = "never", StepSize = 0 }
            }
        };

        var xml = SaeLabelsTemplateXmlSerializer.Serialize(template);
        var parsed = _service.ParseTemplateXml(xml);

        Assert.Equal("SAE", parsed.Brand);
        Assert.Equal(2, parsed.Objects.Count);
        Assert.Equal("Object-barcode", GetObjectType(parsed.Objects[1]));
        Assert.Single(parsed.Variables);
        Assert.Equal("SKU", parsed.Variables[0].Name);
    }

    private string GetObjectType(TemplateObject obj) => obj switch {
        TextObject => "Object-text",
        BarcodeObject => "Object-barcode",
        _ => "unknown"
    };

    [Fact]
    public void Deserialize_NormalizesVariableTypeAliases()
    {
        const string xml = """
<SaeLabels version="1.0">
  <template brand="SAE" description="Demo" part="P-1" size="custom">
    <label_rectangle width_pt="100" height_pt="50" round_pt="0" x_waste_pt="0" y_waste_pt="0">
        <layout dx_pt="0" dy_pt="0" nx="1" ny="1" x0_pt="0" y0_pt="0" />
    </label_rectangle>
  </template>
  <objects />
  <variables>
    <variable name="PRICE" type="float" initial="10.5" increment="per_page" step="0.2" />
  </variables>
</SaeLabels>
""";

        var parsed = _service.ParseTemplateXml(xml);

        Assert.Single(parsed.Variables);
        Assert.Equal("floating_point", parsed.Variables[0].Type);
        Assert.Equal("per_page", parsed.Variables[0].Increment);
        Assert.Equal(0.2, parsed.Variables[0].StepSize, 6);
    }

    [Fact]
    public void Deserialize_ThrowsForInvalidVariableType()
    {
        const string xml = """
<SaeLabels version="1.0">
  <template brand="SAE" description="Demo" part="P-1" size="custom">
    <label_rectangle width_pt="100" height_pt="50" round_pt="0" x_waste_pt="0" y_waste_pt="0">
        <layout dx_pt="0" dy_pt="0" nx="1" ny="1" x0_pt="0" y0_pt="0" />
    </label_rectangle>
  </template>
  <objects />
  <variables>
    <variable name="X" type="money" initial="10" increment="never" step="0" />
  </variables>
</SaeLabels>
""";

        var ex = Assert.Throws<InvalidDataException>(() => _service.ParseTemplateXml(xml));
        Assert.Contains("Tipo de variable inválido", ex.Message);
    }
}
