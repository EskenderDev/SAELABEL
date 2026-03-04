using Microsoft.Extensions.Logging;
using SAELABEL.Core.Labels.Helpers;
using SAELABEL.Core.Labels.Modelos;
using System.Text;

namespace SAELABEL.Core.Labels.Servicios
{
    public class PrinterOptimizer
    {
        private readonly ILogger<PrinterOptimizer>? _logger;

        public PrinterOptimizer(ILogger<PrinterOptimizer>? logger = null)
        {
            _logger = logger;
        }

        public string GenerateZPL(SaeLabelsTemplate template, Dictionary<string, string> data)
        {
            try
            {
                var zplBuilder = new StringBuilder();

                zplBuilder.AppendLine("^XA");
                zplBuilder.AppendLine("^MMT"); // Modo métrico

                var widthDots = (int)(UnitConverter.PointsToMillimeters(template.LabelRectangle.Width) * 10);
                var heightDots = (int)(UnitConverter.PointsToMillimeters(template.LabelRectangle.Height) * 10);

                zplBuilder.AppendLine($"^PW{widthDots}");
                zplBuilder.AppendLine($"^LL{heightDots}");
                zplBuilder.AppendLine("^LH0,0");

                double scaleFactor = 1;

                foreach (var obj in template.Objects.OrderBy(o => o.Y))
                {
                    try
                    {
                        switch (obj)
                        {
                            case TextObject textObj:
                                AddTextToZPL(zplBuilder, textObj, data, scaleFactor);
                                break;
                            case BarcodeObject barcodeObj:
                                AddBarcodeToZPL(zplBuilder, barcodeObj, data, scaleFactor);
                                break;
                            case BoxObject boxObj:
                                AddBoxToZPL(zplBuilder, boxObj, scaleFactor);
                                break;
                            case LineObject lineObj:
                                AddLineToZPL(zplBuilder, lineObj, scaleFactor);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, $"Error procesando objeto para ZPL: {obj.GetType().Name}");
                    }
                }

                zplBuilder.AppendLine("^XZ");
                return zplBuilder.ToString();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error generando ZPL");
                throw;
            }
        }

        private void AddTextToZPL(StringBuilder zpl, TextObject textObj, Dictionary<string, string> data, double scaleFactor = 1.0)
        {
            var text = ReplaceVariables(textObj.Content, data);
            if (string.IsNullOrEmpty(text)) return;

            var x = (int)(UnitConverter.PointsToMillimeters(textObj.X) * 10 * scaleFactor);
            var y = (int)(UnitConverter.PointsToMillimeters(textObj.Y) * 10 * scaleFactor);

            var fontSizePoints = textObj.FontSize * scaleFactor;
            var fontSizeZPL = (int)(UnitConverter.PointsToMillimeters(fontSizePoints) * 10);

            var zplFont = fontSizePoints switch
            {
                < 8 => "A",   // Muy pequeño
                < 12 => "B",  // Pequeño
                < 18 => "D",  // Normal
                < 24 => "E",  // Grande
                < 30 => "F",  // Muy grande
                _ => "G"      // Extra grande
            };

            zpl.AppendLine($"^FO{x},{y}");
            zpl.AppendLine($"^A{zplFont}N,{fontSizeZPL},{fontSizeZPL}");

            var encodedText = EncodeZPLText(text);
            zpl.AppendLine($"^FD{encodedText}^FS");
        }

        private void AddBarcodeToZPL(StringBuilder zpl, BarcodeObject barcodeObj, Dictionary<string, string> data, double scaleFactor = 1.0)
        {
            var barcodeData = ReplaceVariables(barcodeObj.Data, data);
            if (string.IsNullOrEmpty(barcodeData)) return;

            var x = (int)(UnitConverter.PointsToMillimeters(barcodeObj.X) * 10 * scaleFactor);
            var y = (int)(UnitConverter.PointsToMillimeters(barcodeObj.Y) * 10 * scaleFactor);
            var height = (int)(UnitConverter.PointsToMillimeters(barcodeObj.Height) * 10 * scaleFactor);
            var width = (int)(UnitConverter.PointsToMillimeters(barcodeObj.Width) * 10 * scaleFactor);

            height = Math.Max(height, 30); // Mínimo 3mm

            var barcodeType = barcodeObj.BarcodeType.ToUpper() switch
            {
                "CODE39" => "3",
                "CODE128" => "BC",
                "EAN13" => "E0",
                "EAN8" => "E4",
                "UPC_A" => "UA",
                "UPC_E" => "UE",
                "QR_CODE" => "BQ",
                "DATAMATRIX" => "BX",
                _ => "BC"
            };

            zpl.AppendLine($"^FO{x},{y}");

            var moduleWidth = Math.Max(2, (int)(2 * scaleFactor));
            zpl.AppendLine($"^BY{moduleWidth},3,{height}");

            if (barcodeType == "BQ") // QR Code
            {
                zpl.AppendLine($"^BQN,2,10");
                zpl.AppendLine($"^FDMM,A{barcodeData}^FS");
            }
            else
            {
                zpl.AppendLine($"^B{barcodeType}N,,Y,N");
                zpl.AppendLine($"^FD{barcodeData}^FS");
            }

            if (barcodeObj.ShowText)
            {
                var textY = y + height + 15;
                zpl.AppendLine($"^FO{x},{textY}");
                zpl.AppendLine($"^A0N,20,20");
                zpl.AppendLine($"^FD{barcodeData}^FS");
            }
        }

        private void AddBoxToZPL(StringBuilder zpl, BoxObject boxObj, double scaleFactor = 1.0)
        {
            var x = (int)(UnitConverter.PointsToMillimeters(boxObj.X) * 10 * scaleFactor);
            var y = (int)(UnitConverter.PointsToMillimeters(boxObj.Y) * 10 * scaleFactor);
            var width = (int)(UnitConverter.PointsToMillimeters(boxObj.Width) * 10 * scaleFactor);
            var height = (int)(UnitConverter.PointsToMillimeters(boxObj.Height) * 10 * scaleFactor);
            var lineWidth = Math.Max(2, (int)(UnitConverter.PointsToMillimeters(boxObj.LineWidth) * 10 * scaleFactor));

            zpl.AppendLine($"^FO{x},{y}");
            zpl.AppendLine($"^GB{width},{height},{lineWidth},B^FS");
        }

        private void AddLineToZPL(StringBuilder zpl, LineObject lineObj, double scaleFactor = 1.0)
        {
            var startX = (int)(UnitConverter.PointsToMillimeters(lineObj.X) * 10 * scaleFactor);
            var startY = (int)(UnitConverter.PointsToMillimeters(lineObj.Y) * 10 * scaleFactor);
            var endX = (int)(UnitConverter.PointsToMillimeters(lineObj.X + lineObj.Dx) * 10 * scaleFactor);
            var endY = (int)(UnitConverter.PointsToMillimeters(lineObj.Y + lineObj.Dy) * 10 * scaleFactor);
            var lineWidth = Math.Max(2, (int)(UnitConverter.PointsToMillimeters(lineObj.LineWidth) * 10 * scaleFactor));

            var width = Math.Abs(endX - startX);
            var height = Math.Abs(endY - startY);

            if (width > height) // Línea horizontal
            {
                zpl.AppendLine($"^FO{startX},{startY}");
                zpl.AppendLine($"^GB{width},{lineWidth},{lineWidth}^FS");
            }
            else // Línea vertical
            {
                zpl.AppendLine($"^FO{startX},{startY}");
                zpl.AppendLine($"^GB{lineWidth},{height},{lineWidth}^FS");
            }
        }

        private string ReplaceVariables(string content, Dictionary<string, string> data)
        {
            if (string.IsNullOrEmpty(content)) return content;

            foreach (var variable in data)
            {
                content = content.Replace($"${{{variable.Key}}}", variable.Value);
            }
            return content;
        }

        private string EncodeZPLText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            return text.Replace("^", @"\5E")
                      .Replace("~", @"\7E")
                      .Replace(@"\", @"\5C");
        }

        public bool PrintRawZPL(string zplContent, string printerName)
        {
            try
            {
                return RawPrinterHelper.SendStringToPrinter(printerName, zplContent);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error enviando ZPL a la impresora");
                return false;
            }
        }
    }
}

