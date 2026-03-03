using SAELABEL.Core.Labels.Modelos;
using SAELABEL.Core.Labels.Servicios;
using System.Drawing;

namespace SAELABEL.Core.Labels.Helpers
{
    public static class LabelLayoutHelper
    {
        public static RectangleF CalculateRenderArea(TemplateObject obj, RectangleF renderArea, double scaleX, double scaleY)
        {
            var x = renderArea.X + obj.X * scaleX;
            var y = renderArea.Y + obj.Y * scaleY;
            var width = obj.Width * scaleX;
            var height = obj.Height * scaleY;

            return new RectangleF((float)x, (float)y, (float)width, (float)height);
        }

        public static (int x, int y, int width, int height) ToZplUnits(TemplateObject obj)
        {
            var x = (int)UnitConverter.PointsToMillimeters(obj.X) * 10;
            var y = (int)UnitConverter.PointsToMillimeters(obj.Y) * 10;
            var width = (int)UnitConverter.PointsToMillimeters(obj.Width) * 10;
            var height = (int)UnitConverter.PointsToMillimeters(obj.Height) * 10;

            return (x, y, width, height);
        }

        public static (int x, int y) ToZplPosition(double px, double py)
        {
            var x = (int)UnitConverter.PointsToMillimeters(px) * 10;
            var y = (int)UnitConverter.PointsToMillimeters(py) * 10;
            return (x, y);
        }
    }
}

