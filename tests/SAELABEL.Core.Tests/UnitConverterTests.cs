using SAELABEL.Core.Labels.Servicios;
using Xunit;

namespace SAELABEL.Core.Tests
{
    public class UnitConverterTests
    {
        [Theory]
        [InlineData(72, 25.4)] // 72 pt = 1 inch = 25.4 mm
        [InlineData(0, 0)]
        public void PointsToMillimeters_ShouldConvertCorrectly(double points, double expectedMm)
        {
            var result = UnitConverter.PointsToMillimeters(points);
            Assert.Equal(expectedMm, result, 2);
        }

        [Theory]
        [InlineData(25.4, 72)]
        [InlineData(0, 0)]
        public void MillimetersToPoints_ShouldConvertCorrectly(double mm, double expectedPoints)
        {
            var result = UnitConverter.MillimetersToPoints(mm);
            Assert.Equal(expectedPoints, result, 2);
        }

        [Theory]
        [InlineData("10mm", 28.346)] // 10mm * 2.83465...
        [InlineData("72pt", 72)]
        [InlineData("1in", 72)]
        [InlineData("10", 10)] // Default pt
        public void ParseMeasurement_ShouldParseCorrectly(string input, double expectedPoints)
        {
            var result = UnitConverter.ParseMeasurement(input);
            Assert.Equal(expectedPoints, result, 2);
        }
    }
}

