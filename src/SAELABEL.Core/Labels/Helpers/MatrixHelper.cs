using SAELABEL.Core.Labels.Modelos;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.Versioning;

namespace SAELABEL.Core.Labels.Helpers
{
    [SupportedOSPlatform("windows")]
    public static class MatrixHelper
    {
        public static bool IsIdentity(TransformationMatrix matrix)
        {
            return matrix != null &&
                   matrix.A == 1 && matrix.B == 0 &&
                   matrix.C == 0 && matrix.D == 1 &&
                   matrix.E == 0 && matrix.F == 0;
        }

        public static Matrix CreateTransformationMatrix(
            TransformationMatrix matrix, float x, float y, float width, float height)
        {
            if (matrix == null || IsIdentity(matrix))
                return new Matrix(); // Matriz identidad

            // Crear matriz de transformación 3x3 usando las propiedades correctas
            // La matriz en System.Drawing.Drawing2D.Matrix usa este formato:
            // [A, B, 0]
            // [C, D, 0]
            // [E, F, 1]
            var transform = new Matrix(
                (float)matrix.A, (float)matrix.B,
                (float)matrix.C, (float)matrix.D,
                (float)matrix.E, (float)matrix.F
            );

            return transform;
        }

        public static void ApplyTransformations(Graphics g, TemplateObject obj, float x, float y)
        {
            // Aplicar rotación si está configurada
            if (obj.Rotate && obj.RotationAngle != 0)
            {
                float centerX = x + (float)obj.Width / 2;
                float centerY = y + (float)obj.Height / 2;
                g.TranslateTransform(centerX, centerY);
                g.RotateTransform(obj.RotationAngle);
                g.TranslateTransform(-centerX, -centerY);
            }

            // Aplicar matriz de transformación si está configurada y no es identidad
            if (obj.Matrix != null && !IsIdentity(obj.Matrix))
            {
                var matrix = CreateTransformationMatrix(obj.Matrix, x, y, (float)obj.Width, (float)obj.Height);
                g.MultiplyTransform(matrix);
            }
        }

        public static PointF[] TransformRectangle(TransformationMatrix matrix, float x, float y, float width, float height)
        {
            if (matrix == null || IsIdentity(matrix))
            {
                return new PointF[]
                {
                    new PointF(x, y),
                    new PointF(x + width, y),
                    new PointF(x, y + height),
                    new PointF(x + width, y + height)
                };
            }

            // Aplicar transformación a los cuatro puntos del rectángulo
            var points = new PointF[]
            {
                new PointF(x, y),
                new PointF(x + width, y),
                new PointF(x, y + height),
                new PointF(x + width, y + height)
            };

            var transform = CreateTransformationMatrix(matrix, x, y, width, height);
            transform.TransformPoints(points);

            return points;
        }

        public static RectangleF GetTransformedBounds(TransformationMatrix matrix, float x, float y, float width, float height)
        {
            if (matrix == null || IsIdentity(matrix))
                return new RectangleF(x, y, width, height);

            var points = TransformRectangle(matrix, x, y, width, height);

            var minX = points.Min(p => p.X);
            var minY = points.Min(p => p.Y);
            var maxX = points.Max(p => p.X);
            var maxY = points.Max(p => p.Y);

            return new RectangleF(minX, minY, maxX - minX, maxY - minY);
        }

        public static PointF TransformPoint(TransformationMatrix matrix, float x, float y)
        {
            if (matrix == null || IsIdentity(matrix))
                return new PointF(x, y);

            var point = new PointF[] { new PointF(x, y) };
            var transform = CreateTransformationMatrix(matrix, x, y, 0, 0);
            transform.TransformPoints(point);

            return point[0];
        }

        public static float[] GetTransformationValues(TransformationMatrix matrix)
        {
            if (matrix == null)
                return new float[] { 1, 0, 0, 1, 0, 0 };

            return new float[]
            {
                (float)matrix.A,
                (float)matrix.B,
                (float)matrix.C,
                (float)matrix.D,
                (float)matrix.E,
                (float)matrix.F
            };
        }
    }
}

