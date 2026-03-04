using Microsoft.Extensions.Logging;
using SAE.STUDIO.Core.Labels.Helpers;
using SAE.STUDIO.Core.Labels.Modelos;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.Globalization;
using System.Runtime.Versioning;
using System.Text;
using ZXing;
using ZXing.Common;
using WrapMode = SAE.STUDIO.Core.Labels.Modelos.WrapMode;

namespace SAE.STUDIO.Core.Labels.Servicios
{
    [SupportedOSPlatform("windows")]
    public class LabelRenderer : ILabelRenderer
    {
        private readonly ILogger<LabelRenderer> _logger;
        private readonly PrinterOptimizer _optimizer;

        public LabelRenderer(ILogger<LabelRenderer> logger, PrinterOptimizer optimizer)
        {
            _logger = logger;
            _optimizer = optimizer;
        }

        #region Implementación ILabelRenderer

        public string GenerateZpl(SaeLabelsTemplate template, Dictionary<string, string> data)
        {
            return _optimizer.GenerateZPL(template, data);
        }

        public async Task<string> GenerateZplWithCopiesAsync(SaeLabelsTemplate template, Dictionary<string, string> data, int copies = 1)
        {
            return await Task.Run(() =>
            {
                var sb = new StringBuilder();
                var dataCopy = new Dictionary<string, string>(data);

                // Inicializar variables incrementales
                IncrementalVariableHelper.InitializeIncrementalVariables(template);

                for (int i = 1; i <= copies; i++)
                {
                    // Procesar variables para esta copia
                    var processedData = IncrementalVariableHelper.ProcessIncrementalVariables(template, dataCopy, currentCopy: i, currentItem: i, currentPage: i);

                    // Generar ZPL para esta etiqueta
                    sb.Append(_optimizer.GenerateZPL(template, processedData));
                }

                // Actualizar estado de variables después de imprimir todas las copias
                IncrementalVariableHelper.UpdateIncrementalVariables(template, copies);

                return sb.ToString();
            });
        }

        public async Task<bool> PrintToPrinterAsync(SaeLabelsTemplate template, Dictionary<string, string> data, string printerName, int copies)
        {
            try
            {
                if (IsZplPrinter(printerName))
                {
                    var zpl = await GenerateZplWithCopiesAsync(template, data, copies);
                    return _optimizer.PrintRawZPL(zpl, printerName);
                }
                else
                {
                    // Impresión nativa de Windows (GDI+)
                    if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                    {
                        return PrintWithWindowsNative(template, data, printerName, copies);
                    }
                    else
                    {
                        // Fallback para Linux/Mac usando lp (requiere configuración)
                        return await PrintWithSystemCommand(template, data, printerName, copies);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error imprimiendo en {printerName}");
                return false;
            }
        }

        public async Task<bool> PrintMultipleItemsAsync(
            SaeLabelsTemplate template,
            IEnumerable<Dictionary<string, string>> itemsData,
            string printerName,
            int copiesPerItem = 1)
        {
            try
            {
                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                {
                    return PrintBatchWithWindowsNative(template, itemsData, printerName, copiesPerItem);
                }
                else
                {
                    // Fallback para Unix: imprimir uno por uno
                    int itemsProcessed = 0;
                    IncrementalVariableHelper.InitializeIncrementalVariables(template);

                    foreach (var itemData in itemsData)
                    {
                        itemsProcessed++;
                        var processedData = IncrementalVariableHelper.ProcessIncrementalVariables(
                            template,
                            itemData,
                            1, 
                            itemsProcessed,
                            itemsProcessed); // Propagar itemsProcessed como currentPage también para el fallback

                        await PrintToPrinterAsync(template, processedData, printerName, copiesPerItem);
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en impresión por lotes");
                return false;
            }
        }

        public async Task<byte[]> RenderToImageAsync(SaeLabelsTemplate template, Dictionary<string, string> data, string format = "png")
        {
            // Validar formato
            ValidateImageFormat(format);

            using var bitmap = RenderToBitmap(template, data);
            return await ConvertBitmapToImageAsync(bitmap, format);
        }

        public IEnumerable<string> GetInstalledPrinters()
        {
            var printers = new List<string>();
            try
            {
                foreach (string printer in System.Drawing.Printing.PrinterSettings.InstalledPrinters)
                {
                    printers.Add(printer);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo impresoras instaladas");
            }
            return printers;
        }

        #endregion

        #region Renderizado GDI+ (Bitmap)

        public Bitmap RenderToBitmap(SaeLabelsTemplate template, Dictionary<string, string> data, RenderSettings? settings = null)
        {
            settings ??= new RenderSettings();
            data ??= new Dictionary<string, string>();

            // Procesar variables (mock de copia 1, item 1)
            var processedData = IncrementalVariableHelper.ProcessIncrementalVariables(template, data);

            // Calcular dimensiones en píxeles según DPI
            int width = (int)UnitConverter.PointsToPixels(template.LabelRectangle.Width, settings.DPI);
            int height = (int)UnitConverter.PointsToPixels(template.LabelRectangle.Height, settings.DPI);

            // Asegurar dimensiones mínimas
            width = Math.Max(width, settings.MinimumWidth);
            height = Math.Max(height, settings.MinimumHeight);

            var bitmap = new Bitmap(width, height);
            bitmap.SetResolution(settings.DPI, settings.DPI);
            using var g = Graphics.FromImage(bitmap);

            // Configurar calidad
            g.SmoothingMode = settings.AntiAlias ? SmoothingMode.AntiAlias : SmoothingMode.None;
            g.TextRenderingHint = settings.AntiAlias ? System.Drawing.Text.TextRenderingHint.AntiAlias : System.Drawing.Text.TextRenderingHint.SingleBitPerPixel;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // Fondo
            g.Clear(settings.BackgroundColor);

            // Factor de escala (puntos -> píxeles)
            // 72 puntos = 1 pulgada. DPI píxeles = 1 pulgada.
            // Factor = DPI / 72
            float scale = settings.DPI / 72f;

            // Renderizar objetos
            foreach (var command in template.Objects)
            {
                RenderObject(g, command, processedData, scale);
            }

            return bitmap;
        }

        private void RenderObject(Graphics g, TemplateObject obj, Dictionary<string, string> data, float scale)
        {
            // Calcular posición y tamaño en píxeles
            float x = (float)(obj.X * scale);
            float y = (float)(obj.Y * scale);
            float w = (float)(obj.Width * scale);
            float h = (float)(obj.Height * scale);

            // Guardar estado del graphics para transformaciones locales
            var state = g.Save();

            try
            {
                ApplyTransformations(g, obj, x, y, scale);

                switch (obj)
                {
                    case TextObject textObj:
                        RenderText(g, textObj, data, 0, 0, w, h, scale);
                        break;
                    case BarcodeObject barcodeObj:
                        RenderBarcode(g, barcodeObj, data, 0, 0, w, h);
                        break;
                    case BoxObject boxObj:
                        RenderBox(g, boxObj, 0, 0, w, h, scale);
                        break;
                    case LineObject lineObj:
                        RenderLine(g, lineObj, 0, 0, scale);
                        break;
                    case EllipseObject ellipseObj:
                        RenderEllipse(g, ellipseObj, 0, 0, w, h, scale);
                        break;
                    case ImageObject imgObj:
                        RenderImage(g, imgObj, 0, 0, w, h);
                        break;
                    case PathObject pathObj:
                        RenderPath(g, pathObj, 0, 0, w, h, scale);
                        break;
                }
            }
            finally
            {
                g.Restore(state);
            }
        }

        private void RenderText(Graphics g, TextObject textObj, Dictionary<string, string> data, float x, float y, float width, float height, float scale)
        {
            var text = ReplaceVariables(textObj.Content, data);
            if (string.IsNullOrEmpty(text)) return;

            using var brush = new SolidBrush(ParseColor(textObj.Color));

            // Configurar fuente
            using var font = CreateFont(textObj);
            // Escalar fuente según DPI - Usar GraphicsUnit.Pixel para que el tamaño sea en píxeles
            using var scaledFont = new Font(font.FontFamily, font.Size * scale, font.Style, GraphicsUnit.Pixel);

            var format = CreateStringFormat(textObj);

            var rect = new RectangleF(x, y, width, height);

            // Manejo de AutoShrink
            var finalFont = textObj.AutoShrink
                ? AutoShrinkFont(g, text, scaledFont, rect, format)
                : scaledFont;

            // Manejo de sombra
            RenderTextShadow(g, text, finalFont, rect, format, textObj);

            // Manejo de line spacing
            if (textObj.LineSpacing > 0 && Math.Abs(textObj.LineSpacing - 1.0) > 0.01)
            {
                if (textObj.WrapMode == WrapMode.None || textObj.AutoShrink)
                {
                    RenderTextWithLineSpacing(g, text, finalFont, brush, rect, format, textObj.LineSpacing);
                }
                else
                {
                    RenderTextWithWrapAndSpacing(g, text, finalFont, brush, rect, format, textObj.LineSpacing, textObj.WrapMode);
                }
            }
            else
            {
                // Renderizado estándar
                g.DrawString(text, finalFont, brush, rect, format);
            }

            if (textObj.AutoShrink && finalFont != scaledFont)
                finalFont.Dispose();

            format.Dispose();
        }

        #endregion

        #region Impresión Nativa (Windows)

        private bool PrintWithWindowsNative(SaeLabelsTemplate template, Dictionary<string, string> data, string printerName, int copies)
        {
            try
            {
                // Inicializar variables incremental
                IncrementalVariableHelper.InitializeIncrementalVariables(template);

                for (int i = 0; i < copies; i++)
                {
                    using var pd = new PrintDocument();
                    pd.PrinterSettings.PrinterName = printerName;
                    pd.PrinterSettings.Copies = 1;
                    
                    // Importante: No mostrar diálogos de impresión que puedan resetear el tamaño
                    pd.PrintController = new StandardPrintController();

                    // Configurar el tamaño del papel dinámicamente según el template
                    // Centésimas de pulgada (hunderths of an inch)
                    int paperWidth = (int)Math.Round(template.LabelRectangle.Width / 72.0 * 100.0);
                    int paperHeight = (int)Math.Round(template.LabelRectangle.Height / 72.0 * 100.0);
                    
                    var customSize = new PaperSize("Custom Label", paperWidth, paperHeight);
                    pd.DefaultPageSettings.PaperSize = customSize;
                    pd.PrinterSettings.DefaultPageSettings.PaperSize = customSize; // Duplicar para compatibilidad
                    pd.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
                    pd.OriginAtMargins = false; 
                    var processedData = IncrementalVariableHelper.ProcessIncrementalVariables(template, data, currentCopy: i + 1, currentItem: i + 1, currentPage: i + 1);

                    pd.PrintPage += (sender, e) =>
                    {
                        var g = e.Graphics;
                        if (g is null) return;
                        
                        g.PageUnit = GraphicsUnit.Pixel;

                        // Usar el DPI de la impresora para el escalado directo
                        float printerDpiX = g.DpiX;
                        float printerDpiY = g.DpiY;

                        // Calculamos el tamaño final en píxeles reales de la impresora
                        float finalWidthPx = (float)(template.LabelRectangle.Width / 72.0 * printerDpiX);
                        float finalHeightPx = (float)(template.LabelRectangle.Height / 72.0 * printerDpiY);

                        // Renderizar internamente a 300 DPI (calidad)
                        float renderDpi = 300f;
                        using var highResLabel = RenderToBitmap(template, processedData, new RenderSettings
                        {
                            DPI = renderDpi,
                            BackgroundColor = Color.White,
                            AntiAlias = true
                        });

                        // Configurar calidad de estiramiento/dibujo
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                        // Dibujar desde (0,0) ocupando el tamaño calculado
                        // Esto ignora el centrado fallido y usa el área real definida por PaperSize
                        g.DrawImage(highResLabel, 0, 0, finalWidthPx, finalHeightPx);
                        
                        e.HasMorePages = false;
                    };

                    pd.Print();
                }

                IncrementalVariableHelper.UpdateIncrementalVariables(template, copies);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en impresión nativa Windows");
                return false;
            }
        }

        private bool PrintBatchWithWindowsNative(
            SaeLabelsTemplate template,
            IEnumerable<Dictionary<string, string>> itemsData,
            string printerName,
            int copiesPerItem)
        {
            try
            {
                var itemList = itemsData.ToList();
                if (itemList.Count == 0) return true;

                IncrementalVariableHelper.InitializeIncrementalVariables(template);

                using var pd = new System.Drawing.Printing.PrintDocument();
                pd.PrinterSettings.PrinterName = printerName;
                pd.PrinterSettings.Copies = 1; // Manejaremos las copias mediante páginas repetidas
                pd.PrintController = new System.Drawing.Printing.StandardPrintController();

                int paperWidth = (int)Math.Round(template.LabelRectangle.Width / 72.0 * 100.0);
                int paperHeight = (int)Math.Round(template.LabelRectangle.Height / 72.0 * 100.0);
                var customSize = new System.Drawing.Printing.PaperSize("Custom Label", paperWidth, paperHeight);
                pd.DefaultPageSettings.PaperSize = customSize;
                pd.PrinterSettings.DefaultPageSettings.PaperSize = customSize;
                pd.DefaultPageSettings.Margins = new System.Drawing.Printing.Margins(0, 0, 0, 0);
                pd.OriginAtMargins = false;

                int currentItemIndex = 0;
                int currentCopyIndex = 0;

                pd.PrintPage += (sender, e) =>
                {
                    var itemData = itemList[currentItemIndex];
                    var processedData = IncrementalVariableHelper.ProcessIncrementalVariables(
                        template, itemData, currentCopy: currentCopyIndex + 1, currentItem: currentItemIndex + 1, currentPage: (currentItemIndex * copiesPerItem) + currentCopyIndex + 1);

                    var g = e.Graphics;
                    if (g is null) return;

                    g.PageUnit = GraphicsUnit.Pixel;
                    float printerDpiX = g.DpiX;
                    float printerDpiY = g.DpiY;
                    float finalWidthPx = (float)(template.LabelRectangle.Width / 72.0 * printerDpiX);
                    float finalHeightPx = (float)(template.LabelRectangle.Height / 72.0 * printerDpiY);

                    float renderDpi = 300f;
                    using var highResLabel = RenderToBitmap(template, processedData, new RenderSettings
                    {
                        DPI = renderDpi,
                        BackgroundColor = Color.White,
                        AntiAlias = true
                    });

                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    
                    g.DrawImage(highResLabel, 0, 0, finalWidthPx, finalHeightPx);

                    currentCopyIndex++;
                    if (currentCopyIndex >= copiesPerItem)
                    {
                        currentCopyIndex = 0;
                        currentItemIndex++;
                    }

                    e.HasMorePages = currentItemIndex < itemList.Count;
                };

                pd.Print();
                IncrementalVariableHelper.UpdateIncrementalVariables(template, itemList.Count * copiesPerItem);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en impresión por lotes nativa Windows");
                return false;
            }
        }

        private async Task<bool> PrintWithSystemCommand(SaeLabelsTemplate template, Dictionary<string, string> data, string printerName, int copies)
        {
            // Implementación básica para lp (Unix/Linux)
            // Se generaría una imagen temporal y se enviaría a lp
            try
            {
                var imageBytes = await RenderToImageAsync(template, data, "png");
                var tempFile = Path.GetTempFileName() + ".png";
                await File.WriteAllBytesAsync(tempFile, imageBytes);

                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "lp",
                        Arguments = $"-d {printerName} -n {copies} {tempFile}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync();

                File.Delete(tempFile);

                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error imprimiendo con comando del sistema");
                return false;
            }
        }

        #endregion

        #region Métodos privados de Renderizado

        // Copiados logicamente de los métodos vistos anteriormente en el análisis, adaptados a ILogger y contextos

        private void RenderTextMultiline(Graphics g, string text, Font font, Brush brush, RectangleF rect, StringFormat format, double lineSpacing)
        {
            // Implementación simplificada (fallback a DrawString por ahora)
            // Una implementación real necesitaría medir línea por línea como en el código original
            try
            {
                // Usar código original si es posible, aquí simplificado por brevedad en este ejemplo
                if (lineSpacing <= 0 || Math.Abs(lineSpacing - 1.0) < 0.01)
                {
                    g.DrawString(text, font, brush, rect, format);
                    return;
                }

                // Lógica de separación de líneas y dibujado manual...
                RenderTextWithLineSpacing(g, text, font, brush, rect, format, lineSpacing);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en RenderTextMultiline");
                // Fallback: renderizado normal
                g.DrawString(text, font, brush, rect, format);
            }
        }
        private void RenderTextWithWrapAndSpacing(Graphics g, string text, Font font, Brush brush,
    RectangleF rect, StringFormat format, double lineSpacing, WrapMode wrapMode)
        {
            if (string.IsNullOrEmpty(text)) return;

            // Si no hay line spacing personalizado, usar DrawString normal
            if (lineSpacing <= 0 || Math.Abs(lineSpacing - 1.0) < 0.01)
            {
                g.DrawString(text, font, brush, rect, format);
                return;
            }

            // Para wrap automático, necesitamos dividir el texto en líneas manualmente
            var lines = SplitTextIntoLines(g, text, font, rect.Width, format, wrapMode);
            var lineHeight = font.Height * (float)lineSpacing;
            var currentY = rect.Y;

            var lineFormat = new StringFormat
            {
                Alignment = format.Alignment,
                LineAlignment = StringAlignment.Near,
                Trimming = StringTrimming.None
            };

            foreach (var line in lines)
            {
                var lineSize = g.MeasureString(line, font, (int)rect.Width, lineFormat);

                // Verificar si cabe en el área vertical
                if (currentY + lineSize.Height > rect.Bottom)
                    break;

                var lineRect = new RectangleF(rect.X, currentY, rect.Width, lineSize.Height);
                g.DrawString(line, font, brush, lineRect, lineFormat);
                currentY += lineHeight;
            }

            lineFormat.Dispose();
        }
        private void RenderTextWithLineSpacing(Graphics g, string text, Font font, Brush brush,
    RectangleF rect, StringFormat format, double lineSpacing)
        {
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var lineHeight = font.Height * (float)lineSpacing;
            var currentY = rect.Y;

            foreach (var line in lines)
            {
                var lineSize = g.MeasureString(line, font, (int)rect.Width, format);
                var lineRect = new RectangleF(rect.X, currentY, rect.Width, lineSize.Height);

                g.DrawString(line, font, brush, lineRect, format);
                currentY += lineHeight;

                // Si nos salimos del área, salir del bucle
                if (currentY > rect.Bottom)
                    break;
            }
        }

        private List<string> SplitTextIntoLines(Graphics g, string text, Font font, float maxWidth,
    StringFormat format, WrapMode wrapMode)
        {
            var lines = new List<string>();

            if (string.IsNullOrEmpty(text))
                return lines;

            var words = text.Split(' ');
            var currentLine = new StringBuilder();

            foreach (var word in words)
            {
                var testLine = currentLine.Length > 0 ? currentLine + " " + word : word;
                var size = g.MeasureString(testLine, font, (int)maxWidth, format);

                if (size.Width <= maxWidth)
                {
                    currentLine.Append(currentLine.Length > 0 ? " " + word : word);
                }
                else
                {
                    // Agregar la línea actual a la lista
                    if (currentLine.Length > 0)
                    {
                        lines.Add(currentLine.ToString());
                        currentLine.Clear();
                    }

                    // Si la palabra individual es más ancha que el máximo, dividir por caracteres
                    if (wrapMode == WrapMode.Character)
                    {
                        var charLines = SplitWordByCharacters(g, word, font, maxWidth, format);
                        lines.AddRange(charLines);
                    }
                    else
                    {
                        currentLine.Append(word);
                    }
                }
            }

            // Agregar la última línea
            if (currentLine.Length > 0)
                lines.Add(currentLine.ToString());

            return lines;
        }

        private List<string> SplitWordByCharacters(Graphics g, string word, Font font, float maxWidth, StringFormat format)
        {
            var lines = new List<string>();
            var current = new StringBuilder();

            foreach (var c in word)
            {
                var test = current.ToString() + c;
                var size = g.MeasureString(test, font, (int)maxWidth, format);

                if (size.Width <= maxWidth)
                {
                    current.Append(c);
                }
                else
                {
                    if (current.Length > 0)
                        lines.Add(current.ToString());
                    current.Clear();
                    current.Append(c);
                }
            }

            if (current.Length > 0)
                lines.Add(current.ToString());

            return lines;
        }

        private string GetFirstLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var newLineIndex = text.IndexOfAny(new[] { '\r', '\n' });
            return newLineIndex >= 0 ? text.Substring(0, newLineIndex) : text;
        }
        private StringFormat CreateStringFormat(TextObject textObj)
        {
            var format = new StringFormat
            {
                Alignment = textObj.Alignment switch
                {
                    TextAlignment.Center => StringAlignment.Center,
                    TextAlignment.Right => StringAlignment.Far,
                    _ => StringAlignment.Near
                },
                LineAlignment = textObj.VerticalAlignment switch
                {
                    VerticalAlignment.Middle => StringAlignment.Center,
                    VerticalAlignment.Bottom => StringAlignment.Far,
                    _ => StringAlignment.Near
                },
                Trimming = StringTrimming.None,
                FormatFlags = 0
            };

            // Configurar el modo de wrap según la propiedad WrapMode
            switch (textObj.WrapMode)
            {
                case WrapMode.None:
                    format.FormatFlags |= StringFormatFlags.NoWrap;
                    format.Trimming = StringTrimming.None;
                    break;
                case WrapMode.Character:
                    format.FormatFlags &= ~StringFormatFlags.LineLimit;
                    format.Trimming = StringTrimming.Character;
                    break;
                case WrapMode.Word:
                default:
                    format.Trimming = StringTrimming.Word;
                    break;
            }

            return format;
        }

        private void RenderTextShadow(Graphics g, string text, Font font, RectangleF rect, StringFormat format, TextObject textObj)
        {
            if (textObj.Shadow == null || !textObj.Shadow.Enabled) return;

            using var shadowBrush = new SolidBrush(ParseColor(textObj.Shadow.Color));
            var shadowRect = new RectangleF(
                rect.X + (float)textObj.Shadow.OffsetX,
                rect.Y + (float)textObj.Shadow.OffsetY,
                rect.Width,
                rect.Height
            );

            g.DrawString(text, font, shadowBrush, shadowRect, format);
        }

        private Font AutoShrinkFont(Graphics g, string text, Font original, RectangleF rect, StringFormat format)
        {
            float size = original.Size;
            Font testFont = new Font(original.FontFamily, size, original.Style);

            while (size > 4) // Tamaño mínimo de fuente
            {
                var sizeF = g.MeasureString(text, testFont, (int)rect.Width, format);

                // Calcular el line spacing manualmente (aproximadamente 1.2 veces el tamaño de fuente)
                var lineHeight = testFont.Height; // Esto ya incluye el line spacing
                var lineCount = GetLineCount(text, testFont, rect.Width, format);
                var totalHeight = lineHeight * lineCount;

                if (sizeF.Width <= rect.Width && totalHeight <= rect.Height)
                    return testFont;

                size -= 0.5f;
                testFont.Dispose();
                testFont = new Font(original.FontFamily, size, original.Style);
            }

            return testFont;
        }
        private int GetLineCount(string text, Font font, float maxWidth, StringFormat format)
        {
            if (string.IsNullOrEmpty(text)) return 0;

            // Para texto de una sola línea
            if (!text.Contains(' ') && !text.Contains('\n') && !text.Contains('\r'))
                return 1;

            // Usar MeasureString para obtener el número de líneas
            using var testBmp = new Bitmap(1, 1);
            using var testG = Graphics.FromImage(testBmp);

            var size = testG.MeasureString(text, font, (int)maxWidth, format);
            var lineHeight = font.Height;

            // Estimación basada en la altura total
            return (int)Math.Ceiling(size.Height / lineHeight);
        }
        private void ApplyTransformations(Graphics g, TemplateObject obj, float x, float y, float scale)
        {
            float w = (float)(obj.Width * scale);
            float h = (float)(obj.Height * scale);

            // 1. Mover al centro del objeto
            g.TranslateTransform(x + w / 2, y + h / 2);

            // 2. Aplicar matriz de transformación (A, B, C, D) de la UI/Glabels
            if (obj.Matrix != null && !obj.Matrix.IsIdentity)
            {
                using var matrix = new Matrix(
                    (float)obj.Matrix.A, (float)obj.Matrix.B,
                    (float)obj.Matrix.C, (float)obj.Matrix.D,
                    0, 0
                );
                g.MultiplyTransform(matrix);
            }
            else if (obj.RotationAngle != 0)
            {
                // 3. Aplicar rotación explícita SOLO si no hay matriz (la matriz generada por toAffine ya lo incluye)
                g.RotateTransform(obj.RotationAngle);
            }

            // 4. Mover de vuelta para que (0,0) sea la esquina superior izquierda del objeto
            g.TranslateTransform(-w / 2, -h / 2);
        }
        private void RenderBarcode(Graphics g, BarcodeObject barcodeObj, Dictionary<string, string> data, float x, float y, float width, float height)
        {
            var code = ReplaceVariables(barcodeObj.Data, data);
            if (string.IsNullOrEmpty(code))
            {
                _logger.LogWarning($"Código de barras vacío para objeto en ({x}, {y})");
                return;
            }

            _logger.LogDebug($"Renderizando código de barras: {code}, Tipo: {barcodeObj.BarcodeType}, Posición: ({x}, {y}), Tamaño: ({width}x{height})");

            try
            {
                BarcodeFormat format;
                bool needsAsterisks = false;

                switch (barcodeObj.BarcodeType.ToLower())
                {
                    case "code39":
                    case "code_39":
                    case "code-39":
                        format = BarcodeFormat.CODE_39;
                        needsAsterisks = true;
                        break;
                    case "code128":
                    case "code_128":
                    case "code-128":
                        format = BarcodeFormat.CODE_128;
                        break;
                    case "qrcode":
                    case "qr_code":
                    case "qr-code":
                    case "qr":
                        format = BarcodeFormat.QR_CODE;
                        break;
                    case "ean13":
                    case "ean_13":
                    case "ean-13":
                        format = BarcodeFormat.EAN_13;
                        code = code.PadLeft(13, '0').Substring(0, 13);
                        break;
                    case "ean8":
                    case "ean_8":
                    case "ean-8":
                        format = BarcodeFormat.EAN_8;
                        code = code.PadLeft(8, '0').Substring(0, 8);
                        break;
                    case "upc":
                    case "upca":
                    case "upc_a":
                    case "upc-a":
                        format = BarcodeFormat.UPC_A;
                        code = code.PadLeft(12, '0').Substring(0, 12);
                        break;
                    case "upce":
                    case "upc_e":
                    case "upc-e":
                        format = BarcodeFormat.UPC_E;
                        break;
                    case "itf":
                        format = BarcodeFormat.ITF;
                        if (code.Length % 2 != 0) code = "0" + code;
                        break;
                    case "datamatrix":
                    case "data_matrix":
                    case "data-matrix":
                        format = BarcodeFormat.DATA_MATRIX;
                        break;
                    default:
                        _logger.LogWarning($"Tipo de código de barras no reconocido: {barcodeObj.BarcodeType}, usando CODE128 por defecto");
                        format = BarcodeFormat.CODE_128;
                        break;
                }

                if (needsAsterisks && !code.StartsWith("*") && !code.EndsWith("*"))
                {
                    code = "*" + code + "*";
                }

                int minWidth, minHeight;
                if (format == BarcodeFormat.QR_CODE || format == BarcodeFormat.DATA_MATRIX)
                {
                    minWidth = minHeight = 150;
                }
                else
                {
                    minWidth = 200;
                    minHeight = 80;
                }

                int barcodeWidth = Math.Max((int)width, minWidth);
                int barcodeHeight = Math.Max((int)height, minHeight);

                var options = new EncodingOptions
                {
                    Width = barcodeWidth,
                    Height = barcodeHeight,
                    Margin = 10,
                    PureBarcode = !barcodeObj.ShowText
                };

                options.Hints.Add(EncodeHintType.CHARACTER_SET, "UTF-8");

                switch (format)
                {
                    case BarcodeFormat.CODE_128:
                        options.Hints[EncodeHintType.CHARACTER_SET] = "UTF-8";
                        options.Hints[EncodeHintType.MARGIN] = 10;
                        break;

                    case BarcodeFormat.QR_CODE:
                        options.Hints[EncodeHintType.ERROR_CORRECTION] = ZXing.QrCode.Internal.ErrorCorrectionLevel.M;
                        options.Hints[EncodeHintType.CHARACTER_SET] = "UTF-8";
                        options.Hints[EncodeHintType.MARGIN] = 2;
                        break;

                    case BarcodeFormat.DATA_MATRIX:
                        options.Hints[EncodeHintType.CHARACTER_SET] = "UTF-8";
                        options.Hints[EncodeHintType.MARGIN] = 1;
                        break;

                    case BarcodeFormat.EAN_13:
                    case BarcodeFormat.EAN_8:
                    case BarcodeFormat.UPC_A:
                    case BarcodeFormat.UPC_E:
                        options.Hints[EncodeHintType.MARGIN] = 10;
                        break;

                    case BarcodeFormat.CODE_39:
                        options.Hints[EncodeHintType.MARGIN] = 10;
                        break;
                }

                // Crear el writer
                var writer = new BarcodeWriter<Bitmap>
                {
                    Format = format,
                    Options = options,
                    Renderer = new ZXing.Windows.Compatibility.BitmapRenderer()
                };

                _logger.LogDebug($"Intentando generar código con formato {format} y dimensiones {barcodeWidth}x{barcodeHeight}");

                // Generar el código de barras
                Bitmap? bmp = null;
                try
                {
                    bmp = writer.Write(code);
                }
                catch (ArgumentException ex)
                {
                    _logger.LogError(ex, $"Error de validación al generar código de barras: {code}");
                    DrawFallbackBarcode(g, code, x, y, width, height, $"Código inválido: {ex.Message}");
                    return;
                }

                if (bmp == null)
                {
                    _logger.LogError("No se pudo generar la imagen del código de barras");
                    DrawFallbackBarcode(g, code, x, y, width, height, "Error de generación");
                    return;
                }

                // Guardar estado de gráficos
                var state = g.Save();

                try
                {
                    // Mejorar la calidad del renderizado
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;

                    // Dibujar la imagen del código de barras
                    g.DrawImage(bmp, x, y, width, height);

                    _logger.LogDebug($"Código de barras renderizado exitosamente: {code}");
                }
                finally
                {
                    g.Restore(state);
                    bmp?.Dispose();
                }

                // Dibujar el texto si está habilitado
                //if (barcodeObj.ShowText)
                //{
                //    DrawBarcodeText(g, code, x, y, width, height);
                //}
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error general al renderizar código de barras: {code}");
                DrawFallbackBarcode(g, code, x, y, width, height, ex.Message);
            }
        }

        private void DrawBarcodeText(Graphics g, string text, float x, float y, float width, float height)
        {
            try
            {
                // Remover asteriscos de CODE_39 si existen
                var displayText = text.Trim('*');

                using var font = new Font("Arial", Math.Max(8, height * 0.12f), FontStyle.Regular);
                using var brush = new SolidBrush(Color.Black);

                var format = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Near
                };

                var textRect = new RectangleF(x, y + height + 2, width, 20);
                g.DrawString(displayText, font, brush, textRect, format);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al dibujar texto del código de barras");
            }
        }

        private void DrawFallbackBarcode(Graphics g, string code, float x, float y, float width, float height, string errorMessage = "Error desconocido")
        {
            try
            {
                // Dibujar fondo de error
                using var bgBrush = new SolidBrush(Color.FromArgb(255, 255, 230)); // Amarillo claro
                using var borderPen = new Pen(Color.Red, 2);

                g.FillRectangle(bgBrush, x, y, width, height);
                g.DrawRectangle(borderPen, x, y, width, height);

                // Dibujar mensaje de error
                using var font = new Font("Arial", Math.Max(8, height * 0.1f), FontStyle.Bold);
                using var textBrush = new SolidBrush(Color.Red);

                var format = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };

                var rect = new RectangleF(x, y, width, height / 2);
                g.DrawString("ERROR", font, textBrush, rect, format);

                // Dibujar código
                using var codeFont = new Font("Arial", Math.Max(7, height * 0.08f));
                using var codeBrush = new SolidBrush(Color.Black);

                rect.Y = y + height / 2;
                rect.Height = height / 4;
                g.DrawString(code, codeFont, codeBrush, rect, format);

                // Dibujar mensaje
                using var msgFont = new Font("Arial", Math.Max(6, height * 0.06f));
                rect.Y = y + (height * 0.75f);
                rect.Height = height / 4;
                g.DrawString(errorMessage, msgFont, codeBrush, rect, format);

                _logger.LogWarning($"Usando fallback para código de barras: {code} - {errorMessage}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en fallback del código de barras");
                // Último recurso: dibujar solo el texto
                try
                {
                    g.DrawString($"ERROR: {code}", SystemFonts.DefaultFont, Brushes.Red, x, y);
                }
                catch { }
            }
        }
        private void RenderBox(Graphics g, BoxObject box, float x, float y, float width, float height, float scale)
        {
            using var brush = new SolidBrush(ParseColor(box.FillColor));
            using var pen = new Pen(ParseColor(box.LineColor), (float)(box.LineWidth * scale));
            g.FillRectangle(brush, x, y, width, height);
            g.DrawRectangle(pen, x, y, width, height);
        }

        private void RenderLine(Graphics g, LineObject line, float x, float y, float scale)
        {
            var color = ParseColor(line.LineColor);
            if (color == Color.Transparent) return;

            using var pen = new Pen(color, (float)(line.LineWidth * scale));
            // Con el nuevo sistema de transformaciones, x e y siempre serán 0 aquí
            g.DrawLine(pen, x, y, x + (float)line.Dx * scale, y + (float)line.Dy * scale);
        }

        private void RenderEllipse(Graphics g, EllipseObject ellipse, float x, float y, float width, float height, float scale)
        {
            using var brush = new SolidBrush(ParseColor(ellipse.FillColor));
            using var pen = new Pen(ParseColor(ellipse.LineColor), (float)(ellipse.LineWidth * scale));
            g.FillEllipse(brush, x, y, width, height);
            g.DrawEllipse(pen, x, y, width, height);
        }

        private void RenderImage(Graphics g, ImageObject imgObj, float x, float y, float width, float height)
        {
            if (string.IsNullOrEmpty(imgObj.Source)) return;

            try
            {
                using var img = LoadImage(imgObj.Source);
                if (img == null) return;

                // Si hay una matriz de transformación compleja, usar transformación de puntos
                if (imgObj.Matrix != null && !MatrixHelper.IsIdentity(imgObj.Matrix))
                {
                    var points = MatrixHelper.TransformRectangle(imgObj.Matrix, x, y, width, height);
                    g.DrawImage(img, points);
                }
                else if (imgObj.LockAspectRatio)
                {
                    DrawImageAspect(g, img, x, y, width, height);
                }
                else
                {
                    g.DrawImage(img, x, y, width, height);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Error renderizando imagen: {imgObj.Source}");
            }
        }

        private Image? LoadImage(string src)
        {
            if (src.StartsWith("data:image/"))
            {
                var bytes = Convert.FromBase64String(src.Split(',')[1]);
                return Image.FromStream(new MemoryStream(bytes));
            }
            else if (File.Exists(src))
                return Image.FromFile(src);
            return null;
        }

        private void DrawImageAspect(Graphics g, Image img, float x, float y, float w, float h)
        {
            var ratio = (float)img.Width / img.Height;
            float newW, newH;
            if (w / h > ratio) { newH = h; newW = h * ratio; }
            else { newW = w; newH = w / ratio; }
            g.DrawImage(img, x + (w - newW) / 2, y + (h - newH) / 2, newW, newH);
        }


        private void RenderPath(Graphics g, PathObject pathObj, float x, float y, float width, float height, float scale)
        {
            if (string.IsNullOrEmpty(pathObj.Data)) return;

            try
            {
                using var path = new System.Drawing.Drawing2D.GraphicsPath();
                string data = pathObj.Data;
                
                // Tokenizer robusto para comandos SVG Path (M, L, Z, H, V, C, etc.)
                // Este regex captura comandos (una letra) y números (decimales con posible signo)
                var tokens = System.Text.RegularExpressions.Regex.Matches(data, @"([A-Za-z])|([-+]?[0-9]*\.?[0-9]+)");
                
                // El viewBox de los Paths SVG en Glabels/Frontend es 0 0 24 24
                float scaleX = width / 24f;
                float scaleY = height / 24f;

                float currentX = 0, currentY = 0;
                float startX = 0, startY = 0;
                char currentCmd = '\0';
                int tokenIdx = 0;

                while (tokenIdx < tokens.Count)
                {
                    var token = tokens[tokenIdx].Value;
                    if (char.IsLetter(token[0]))
                    {
                        currentCmd = char.ToUpper(token[0]);
                        tokenIdx++;
                    }

                    var pairs = new List<float>();
                    while (tokenIdx < tokens.Count && !char.IsLetter(tokens[tokenIdx].Value[0]))
                    {
                        if (float.TryParse(tokens[tokenIdx].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float val))
                            pairs.Add(val);
                        tokenIdx++;
                    }

                    switch (currentCmd)
                    {
                        case 'M':
                            if (pairs.Count >= 2)
                            {
                                currentX = pairs[0];
                                currentY = pairs[1];
                                startX = currentX; startY = currentY;
                                path.StartFigure();
                            }
                            break;
                        case 'L':
                            for (int i = 0; i < pairs.Count - 1; i += 2)
                            {
                                float nextX = pairs[i];
                                float nextY = pairs[i + 1];
                                // Dibujar relativo a (0,0) escalando coordenadas por viewBox y DPI
                                path.AddLine(currentX * scaleX, currentY * scaleY, nextX * scaleX, nextY * scaleY);
                                currentX = nextX;
                                currentY = nextY;
                            }
                            break;
                        case 'H':
                            foreach (var hVal in pairs)
                            {
                                float nextX = hVal;
                                path.AddLine(currentX * scaleX, currentY * scaleY, nextX * scaleX, currentY * scaleY);
                                currentX = nextX;
                            }
                            break;
                        case 'V':
                            foreach (var vValue in pairs)
                            {
                                float nextY = vValue;
                                path.AddLine(currentX * scaleX, currentY * scaleY, currentX * scaleX, nextY * scaleY);
                                currentY = nextY;
                            }
                            break;
                        case 'Z':
                            if (path.PointCount > 0)
                            {
                                path.AddLine(currentX * scaleX, currentY * scaleY, startX * scaleX, startY * scaleY);
                                path.CloseFigure();
                            }
                            break;
                    }
                }

                if (path.PointCount > 0)
                {
                    var fillColor = ParseColor(pathObj.FillColor);
                    if (fillColor != Color.Transparent)
                    {
                        using var brush = new SolidBrush(fillColor);
                        g.FillPath(brush, path);
                    }

                    var lineColor = ParseColor(pathObj.LineColor);
                    if (lineColor != Color.Transparent)
                    {
                        using var pen = new Pen(lineColor, (float)(pathObj.LineWidth * scale));
                        g.DrawPath(pen, path);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error renderizando path: {pathObj.Data}");
            }
        }

        private string ReplaceVariables(string content, Dictionary<string, string> data)
        {
            if (string.IsNullOrEmpty(content) || data == null || data.Count == 0) return content;
            
            // Usar Regex para un reemplazo más limpio y evitar problemas con caracteres especiales
            return System.Text.RegularExpressions.Regex.Replace(content, @"\$\{([^}]+)\}", match =>
            {
                var key = match.Groups[1].Value;
                return data.TryGetValue(key, out var val) ? val : match.Value;
            });
        }

        private Font CreateFont(TextObject obj)
        {
            FontStyle style = FontStyle.Regular;
            if (obj.FontItalic) style |= FontStyle.Italic;
            if (obj.FontUnderline) style |= FontStyle.Underline;
            if (obj.FontWeight == "bold") style |= FontStyle.Bold;

            string family = obj.FontFamily.ToLower() switch
            {
                "sans" or "sans-serif" => "Arial",
                "serif" => "Times New Roman",
                "monospace" => "Courier New",
                _ => obj.FontFamily
            };

            var font = new Font(family, (float)obj.FontSize, style, GraphicsUnit.Point);

            return font;
        }

        private Color ParseColor(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex) || hex.Equals("none", StringComparison.OrdinalIgnoreCase) || hex.Equals("transparent", StringComparison.OrdinalIgnoreCase)) 
                return Color.Transparent;

            try
            {
                // Remover # si existe
                hex = hex.TrimStart('#');

                // Si tiene 8 caracteres (AARRGGBB o RRGGBBAA depending on format, usually AARRGGBB in .NET but Glabels uses RGBA)
                // Glabels suele usar RRGGBBAA (hex). .NET prefiere ARGB.
                // Asumiremos formato RGBA que es común en web/glabels

                if (hex.Length == 8)
                {
                    uint rgba = Convert.ToUInt32(hex, 16);
                    int r = (int)((rgba >> 24) & 0xFF);
                    int g = (int)((rgba >> 16) & 0xFF);
                    int b = (int)((rgba >> 8) & 0xFF);
                    int a = (int)(rgba & 0xFF);
                    return Color.FromArgb(a, r, g, b);
                }
                else if (hex.Length == 6)
                {
                    int r = int.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                    int g = int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                    int b = int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
                    return Color.FromArgb(255, r, g, b);
                }
            }
            catch
            {
                // Fallback
            }
            return Color.Black;
        }

        //            return Color.FromArgb(a, r, g, b);
        //        }

        //        // Fallback: intentar interpretar como entero y construir color (si alpha resulta 0, forzar 255)
        //        uint value = Convert.ToUInt32(hex, 16);
        //        if (hex.Length <= 6)
        //        {
        //            int r = (int)((value >> 16) & 0xFF);
        //            int g = (int)((value >> 8) & 0xFF);
        //            int b = (int)(value & 0xFF);
        //            return Color.FromArgb(255, r, g, b);
        //        }
        //        else
        //        {
        //            int a = (int)((value >> 24) & 0xFF);
        //            int r = (int)((value >> 16) & 0xFF);
        //            int g = (int)((value >> 8) & 0xFF);
        //            int b = (int)(value & 0xFF);
        //            if (a == 0) a = 255;
        //            return Color.FromArgb(a, r, g, b);
        //        }
        //    }
        //    catch
        //    {
        //        return Color.Transparent;
        //    }
        //}

        private async Task<byte[]> ConvertBitmapToImageAsync(Bitmap bitmap, string format)
        {
            return await Task.Run(() =>
            {
                using var ms = new MemoryStream();
                bitmap.Save(ms, GetImageFormat(format));
                return ms.ToArray();
            });
        }

        private ImageFormat GetImageFormat(string format) => format switch
        {
            "jpg" or "jpeg" => ImageFormat.Jpeg,
            "bmp" => ImageFormat.Bmp,
            "gif" => ImageFormat.Gif,
            "tiff" => ImageFormat.Tiff,
            _ => ImageFormat.Png
        };

        #endregion

        #region Métodos Auxiliares

        private bool IsZplPrinter(string printerName)
        {
            return printerName.EndsWith("ZPL", StringComparison.OrdinalIgnoreCase) ||
                   printerName.Contains("zebra", StringComparison.OrdinalIgnoreCase) ||
                   printerName.Contains("thermal", StringComparison.OrdinalIgnoreCase);
        }

        private RectangleF CalculateCenteredRenderArea(SaeLabelsTemplate template, Rectangle bounds, RectangleF printableArea, float dpiX = 96f, float dpiY = 96f)
        {
            // Convertir dimensiones de template a píxeles usando el DPI proporcionado
            var templateWidthPx = UnitConverter.PointsToPixels(template.LabelRectangle.Width, dpiX);
            var templateHeightPx = UnitConverter.PointsToPixels(template.LabelRectangle.Height, dpiY);

            // Calcular posición centrada
            float x = (float)(printableArea.Left + (printableArea.Width - templateWidthPx) / 2);
            float y = (float)(printableArea.Top + (printableArea.Height - templateHeightPx) / 2);
            
            return new RectangleF(x, y, (float)templateWidthPx, (float)templateHeightPx);
        }
        private void ValidateImageFormat(string format)
        {
            var valid = new[] { "png", "jpg", "jpeg", "bmp", "gif", "tiff" };
            if (!valid.Contains(format)) throw new ArgumentException($"Formato inválido: {format}");
        }

        #endregion
    }

    public class RenderSettings
    {
        public float DPI { get; set; } = 300f;
        public Color BackgroundColor { get; set; } = Color.White;
        public bool AntiAlias { get; set; } = true;
        public int MinimumWidth { get; set; } = 100;
        public int MinimumHeight { get; set; } = 100;
    }
}


