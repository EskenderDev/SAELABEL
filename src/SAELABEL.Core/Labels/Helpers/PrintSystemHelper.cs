using System.Drawing;
using System.Drawing.Printing;
using System.Runtime.Versioning;

namespace SAELABEL.Core.Labels.Helpers
{
    [SupportedOSPlatform("windows")]
    public static class PrintSystemHelper
    {
        public static void PrintImage(Image image, string printerName)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));

            PrintDocument printDoc = new PrintDocument
            {
                PrinterSettings = new PrinterSettings
                {
                    PrinterName = printerName
                }
            };

            printDoc.PrintPage += (sender, e) =>
            {
                if (e.Graphics is null)
                {
                    return;
                }

                // Escalar imagen al área imprimible manteniendo relación de aspecto
                Rectangle marginBounds = e.MarginBounds;
                float scale = Math.Min(
                    (float)marginBounds.Width / image.Width,
                    (float)marginBounds.Height / image.Height);

                int width = (int)(image.Width * scale);
                int height = (int)(image.Height * scale);

                e.Graphics.DrawImage(image, marginBounds.Left, marginBounds.Top, width, height);
            };

            printDoc.Print();
        }
    }
}

