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
}
