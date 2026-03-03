using SAELABEL.Core.SaeLabels;

namespace SAELABEL.Core.Tests;

public class SaeLabelsSerializerTests
{
    [Fact]
    public void SerializeAndDeserialize_Roundtrip_PreservesMainFields()
    {
        var doc = new SaeLabelsDocument
        {
            Version = "1.0",
            Template = new SaeTemplate
            {
                Brand = "SAE",
                Description = "Etiqueta producto",
                Part = "P-100",
                Size = "custom",
                WidthPt = 144,
                HeightPt = 72,
                Layout = new SaeLayout { Nx = 1, Ny = 1 }
            },
            Objects = new List<SaeLabelObject>
            {
                new() { Type = "text", XPt = 10, YPt = 12, WidthPt = 100, HeightPt = 20, Content = "${SKU}", Style = "Arial", Color = "000000FF" },
                new() { Type = "barcode", XPt = 10, YPt = 36, WidthPt = 120, HeightPt = 30, Content = "${SKU}", Style = "code128", ShowText = true }
            },
            Variables = new List<SaeLabelVariable>
            {
                new() { Name = "SKU", Type = "string", InitialValue = "ABC-1", Increment = "never", StepSize = 0 }
            }
        };

        var xml = SaeLabelsSerializer.Serialize(doc);
        var parsed = SaeLabelsSerializer.Deserialize(xml);

        Assert.Equal("1.0", parsed.Version);
        Assert.Equal("SAE", parsed.Template.Brand);
        Assert.Equal(2, parsed.Objects.Count);
        Assert.Equal("barcode", parsed.Objects[1].Type);
        Assert.Single(parsed.Variables);
        Assert.Equal("SKU", parsed.Variables[0].Name);
    }

    [Fact]
    public void Deserialize_NormalizesVariableTypeAliases()
    {
        const string xml = """
<saelabels version="1.0">
  <template brand="SAE" description="Demo" part="P-1" size="custom">
    <label_rectangle width_pt="100" height_pt="50" round_pt="0" x_waste_pt="0" y_waste_pt="0" />
    <layout dx_pt="0" dy_pt="0" nx="1" ny="1" x0_pt="0" y0_pt="0" />
  </template>
  <objects />
  <variables>
    <variable name="PRICE" type="float" initial="10.5" increment="per_page" step="0.2" />
  </variables>
</saelabels>
""";

        var parsed = SaeLabelsSerializer.Deserialize(xml);

        Assert.Single(parsed.Variables);
        Assert.Equal("floating_point", parsed.Variables[0].Type);
        Assert.Equal("per_page", parsed.Variables[0].Increment);
        Assert.Equal(0.2, parsed.Variables[0].StepSize, 6);
    }

    [Fact]
    public void Deserialize_ThrowsForInvalidVariableType()
    {
        const string xml = """
<saelabels version="1.0">
  <template brand="SAE" description="Demo" part="P-1" size="custom">
    <label_rectangle width_pt="100" height_pt="50" round_pt="0" x_waste_pt="0" y_waste_pt="0" />
    <layout dx_pt="0" dy_pt="0" nx="1" ny="1" x0_pt="0" y0_pt="0" />
  </template>
  <objects />
  <variables>
    <variable name="X" type="money" initial="10" increment="never" step="0" />
  </variables>
</saelabels>
""";

        var ex = Assert.Throws<InvalidDataException>(() => SaeLabelsSerializer.Deserialize(xml));
        Assert.Contains("Tipo de variable inválido", ex.Message);
    }
}
