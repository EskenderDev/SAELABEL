using Microsoft.Extensions.Logging;
using SAELABEL.Core.Labels.Modelos;
using SAELABEL.Core.Labels.Helpers;
using SAELABEL.Core.Labels.Caching;
using SAELABEL.Core.Labels.Servicios;
using System.Xml.Linq;
using System.IO;
#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604

namespace SAELABEL.Core.Labels.Servicios
{
    public class GlabelsTemplateService
    {
        private readonly TemplateCache _cache;
        private readonly ILogger _logger;

        public GlabelsTemplateService(ILogger<GlabelsTemplateService> logger, TemplateCache cache)
        {
            _logger = logger;
            _cache = cache;
        }

        public GlabelsTemplate LoadTemplate(string xmlFilePath)
        {
            // Verificar cache primero
            if (_cache.TryGetTemplate(xmlFilePath, out var cachedTemplate))
            {
                return cachedTemplate;
            }

            var doc = XDocument.Load(xmlFilePath);
            var template = ParseDocument(doc, xmlFilePath);

            // Guardar en cache
            _cache.AddTemplate(xmlFilePath, template);

            return template;
        }

        public GlabelsTemplate ParseTemplateXml(string xmlContent, string sourceName = "")
        {
            if (string.IsNullOrWhiteSpace(xmlContent))
            {
                throw new InvalidDataException("El contenido XML está vacío.");
            }

            var doc = XDocument.Parse(xmlContent);
            return ParseDocument(doc, sourceName);
        }

        private GlabelsTemplate ParseDocument(XDocument doc, string sourceName)
        {
            var template = new GlabelsTemplate
            {
                FilePath = sourceName,
                LastModified = string.IsNullOrWhiteSpace(sourceName) || !File.Exists(sourceName)
                    ? DateTime.UtcNow
                    : File.GetLastWriteTime(sourceName)
            };

            // Parsear elemento Template
            var root = doc.Root ?? throw new InvalidDataException("XML sin nodo raíz.");
            var templateElement = root.Name.LocalName == "Template" ? root : root.Element("Template");
            if (templateElement is null)
            {
                throw new InvalidDataException("No se encontró el nodo Template en el XML.");
            }
            template.Brand = (string?)templateElement.Attribute("brand") ?? string.Empty;
            template.Description = (string?)templateElement.Attribute("description") ?? string.Empty;
            template.Part = (string?)templateElement.Attribute("part") ?? string.Empty;
            template.Size = (string?)templateElement.Attribute("size") ?? string.Empty;

            // Parsear meta información
            var metaElement = templateElement.Element("Meta");
            if (metaElement != null)
            {
                template.ProductUrl = (string?)metaElement.Attribute("product_url") ?? string.Empty;
            }

            // Parsear Label-rectangle
            var rectElement = templateElement.Element("Label-rectangle")
                ?? throw new InvalidDataException("No se encontró Label-rectangle en la plantilla.");
            template.LabelRectangle = new LabelRectangle
            {
                Width = UnitConverter.ParseMeasurement((string?)rectElement.Attribute("width") ?? "0pt"),
                Height = UnitConverter.ParseMeasurement((string?)rectElement.Attribute("height") ?? "0pt"),
                Round = UnitConverter.ParseMeasurement((string?)rectElement.Attribute("round") ?? "0pt"),
                XWaste = UnitConverter.ParseMeasurement((string?)rectElement.Attribute("x_waste") ?? "0pt"),
                YWaste = UnitConverter.ParseMeasurement((string?)rectElement.Attribute("y_waste") ?? "0pt")
            };

            // Parsear Layout
            var layoutElement = rectElement.Element("Layout");
            template.LabelRectangle.Layout = new Layout
            {
                Dx = UnitConverter.ParseMeasurement((string?)layoutElement?.Attribute("dx") ?? "0pt"),
                Dy = UnitConverter.ParseMeasurement((string?)layoutElement?.Attribute("dy") ?? "0pt"),
                Nx = (int?)layoutElement?.Attribute("nx") ?? 1,
                Ny = (int?)layoutElement?.Attribute("ny") ?? 1,
                X0 = UnitConverter.ParseMeasurement((string?)layoutElement?.Attribute("x0") ?? "0pt"),
                Y0 = UnitConverter.ParseMeasurement((string?)layoutElement?.Attribute("y0") ?? "0pt")
            };

            // Parsear Objects
            var objectsElement = root.Element("Objects");
            template.Objects = ParseObjects(objectsElement);

            // Parsear Variables
            var variablesElement = root.Element("Variables");
            if (variablesElement != null)
            {
                template.Variables = ParseVariables(variablesElement);
            }

            return template;
        }

        private List<TemplateObject> ParseObjects(XElement? objectsElement)
        {
            var objects = new List<TemplateObject>();
            if (objectsElement is null)
            {
                return objects;
            }

            var rotate = (bool?)objectsElement.Attribute("rotate") ?? false;

            foreach (var objElement in objectsElement.Elements())
            {
                var obj = ParseSingleObject(objElement);
                if (obj != null)
                {
                    obj.Rotate = rotate;
                    objects.Add(obj);
                }
            }

            return objects;
        }

        private TemplateObject? ParseSingleObject(XElement element)
        {
            var commonProps = ParseCommonProperties(element);

            return element.Name.LocalName switch
            {
                "Object-text" => ParseTextObject(element, commonProps),
                "Object-barcode" => ParseBarcodeObject(element, commonProps),
                "Object-box" => ParseBoxObject(element, commonProps),
                "Object-line" => ParseLineObject(element, commonProps),
                "Object-ellipse" => ParseEllipseObject(element, commonProps),
                "Object-image" => ParseImageObject(element, commonProps),
                _ => null!
            };
        }

        private CommonObjectProperties ParseCommonProperties(XElement element)
        {
            return new CommonObjectProperties
            {
                X = UnitConverter.ParseMeasurement((string?)element.Attribute("x") ?? "0pt"),
                Y = UnitConverter.ParseMeasurement((string?)element.Attribute("y") ?? "0pt"),
                Width = UnitConverter.ParseMeasurement((string?)element.Attribute("w") ?? "0pt"),
                Height = UnitConverter.ParseMeasurement((string?)element.Attribute("h") ?? "0pt"),
                LockAspectRatio = (bool?)element.Attribute("lock_aspect_ratio") ?? false,
                Matrix = ParseMatrix(element),
                Shadow = ParseShadow(element)
            };
        }

        private TransformationMatrix ParseMatrix(XElement element)
        {
            // Usar las propiedades correctas: A, B, C, D, E, F
            return new TransformationMatrix
            {
                A = (double?)element.Attribute("a0") ?? 1,  // a0 -> A
                B = (double?)element.Attribute("a1") ?? 0,  // a1 -> B
                C = (double?)element.Attribute("a2") ?? 0,  // a2 -> C
                D = (double?)element.Attribute("a3") ?? 1,  // a3 -> D
                E = (double?)element.Attribute("a4") ?? 0,  // a4 -> E
                F = (double?)element.Attribute("a5") ?? 0   // a5 -> F
            };
        }

        private ShadowEffect? ParseShadow(XElement element)
        {
            var shadowEnabled = (bool?)element.Attribute("shadow") ?? false;
            if (!shadowEnabled) return null;

            return new ShadowEffect
            {
                Enabled = true,
                Color = (string)element.Attribute("shadow_color"),
                Opacity = (double?)element.Attribute("shadow_opacity") ?? 0.5,
                OffsetX = UnitConverter.ParseMeasurement((string)element.Attribute("shadow_x")),
                OffsetY = UnitConverter.ParseMeasurement((string)element.Attribute("shadow_y"))
            };
        }

        // Métodos específicos para cada tipo de objeto...
        private TextObject ParseTextObject(XElement element, CommonObjectProperties common)
        {
            var textObj = new TextObject();
            ApplyCommonProperties(textObj, common);

            textObj.Content = element.Element("p")?.Value ?? element.Value ?? string.Empty;
            textObj.FontFamily = (string)element.Attribute("font_family") ?? "Sans";
            textObj.FontSize = (double?)element.Attribute("font_size") ?? 10;
            textObj.Color = (string)element.Attribute("color") ?? "000000FF"; // Negro por defecto
            textObj.Alignment = ParseTextAlignment((string)element.Attribute("align"));
            textObj.VerticalAlignment = ParseVerticalAlignment((string)element.Attribute("valign"));
            textObj.FontItalic = (bool?)element.Attribute("font_italic") ?? false;
            textObj.FontUnderline = (bool?)element.Attribute("font_underline") ?? false;
            textObj.FontWeight = (string)element.Attribute("font_weight") ?? "normal";
            textObj.LineSpacing = (double?)element.Attribute("line_spacing") ?? 1;
            textObj.AutoShrink = (bool?)element.Attribute("auto_shrink") ?? false;
            textObj.WrapMode = ParseWrapMode((string)element.Attribute("wrap"));

            return textObj;
        }

        private BarcodeObject ParseBarcodeObject(XElement element, CommonObjectProperties common)
        {
            var barcodeObj = new BarcodeObject();
            ApplyCommonProperties(barcodeObj, common);

            barcodeObj.Data = (string)element.Attribute("data");
            barcodeObj.BarcodeType = (string)element.Attribute("style") ?? "code39";
            barcodeObj.ShowText = (bool?)element.Attribute("text") ?? true;
            barcodeObj.Checksum = (bool?)element.Attribute("checksum") ?? true;
            barcodeObj.Color = (string)element.Attribute("color") ?? "000000FF"; // Negro por defecto
            barcodeObj.Backend = (string)element.Attribute("backend");

            return barcodeObj;
        }

        private BoxObject ParseBoxObject(XElement element, CommonObjectProperties common)
        {
            var boxObj = new BoxObject();
            ApplyCommonProperties(boxObj, common);

            boxObj.FillColor = (string)element.Attribute("fill_color") ?? "FFFFFFFF"; // Blanco por defecto
            boxObj.LineColor = (string)element.Attribute("line_color") ?? "000000FF"; // Negro por defecto
            boxObj.LineWidth = UnitConverter.ParseMeasurement((string)element.Attribute("line_width") ?? "1pt");

            return boxObj;
        }

        private LineObject ParseLineObject(XElement element, CommonObjectProperties common)
        {
            var lineObj = new LineObject();
            ApplyCommonProperties(lineObj, common);

            lineObj.Dx = UnitConverter.ParseMeasurement((string)element.Attribute("dx"));
            lineObj.Dy = UnitConverter.ParseMeasurement((string)element.Attribute("dy"));
            lineObj.LineColor = (string)element.Attribute("line_color") ?? "000000FF"; // Negro por defecto
            lineObj.LineWidth = UnitConverter.ParseMeasurement((string)element.Attribute("line_width") ?? "1pt");

            return lineObj;
        }

        private EllipseObject ParseEllipseObject(XElement element, CommonObjectProperties common)
        {
            var ellipseObj = new EllipseObject();
            ApplyCommonProperties(ellipseObj, common);

            ellipseObj.FillColor = (string)element.Attribute("fill_color") ?? "FFFFFFFF"; // Blanco por defecto
            ellipseObj.LineColor = (string)element.Attribute("line_color") ?? "000000FF"; // Negro por defecto
            ellipseObj.LineWidth = UnitConverter.ParseMeasurement((string)element.Attribute("line_width") ?? "1pt");

            return ellipseObj;
        }

        private ImageObject ParseImageObject(XElement element, CommonObjectProperties common)
        {
            var imageObj = new ImageObject();
            ApplyCommonProperties(imageObj, common);

            imageObj.Source = (string)element.Attribute("src");
            imageObj.LockAspectRatio = (bool?)element.Attribute("lock_aspect_ratio") ?? true;

            return imageObj;
        }

        private List<TemplateVariable> ParseVariables(XElement variablesElement)
        {
            var variables = new List<TemplateVariable>();

            if (variablesElement == null)
                return variables;

            foreach (var varElement in variablesElement.Elements("Variable"))
            {
                try
                {
                    var name = varElement.Attribute("name")?.Value;
                    if (string.IsNullOrEmpty(name))
                    {
                        _logger?.LogError("Elemento Variable sin atributo 'name' válido. Se omitirá.");
                        continue;
                    }

                    // Parsear StepSize
                    var stepSizeValue = varElement.Attribute("stepSize")?.Value;
                    int stepSize = 0;
                    if (!string.IsNullOrEmpty(stepSizeValue))
                    {
                        if (!int.TryParse(stepSizeValue, out stepSize))
                        {
                            _logger?.LogWarning($"StepSize no válido '{stepSizeValue}' para variable '{name}'. Usando 0.");
                        }
                    }

                    // Validar y normalizar increment
                    var incrementValue = varElement.Attribute("increment")?.Value ?? "never";
                    incrementValue = NormalizeIncrementValue(incrementValue);

                    // Parsear valor inicial
                    var initialValue = varElement.Attribute("initialValue")?.Value ?? "0";
                    int currentValue = 0;
                    if (int.TryParse(initialValue, out int parsed))
                    {
                        currentValue = parsed;
                    }

                    var variable = new TemplateVariable
                    {
                        Name = name,
                        Type = varElement.Attribute("type")?.Value ?? "integer",
                        InitialValue = initialValue,
                        CurrentValue = currentValue,
                        StepSize = stepSize,
                        Increment = incrementValue
                    };

                    variables.Add(variable);

                    _logger?.LogInformation($"Variable parseada: {name}, Increment: {incrementValue}, StepSize: {stepSize}, Initial: {initialValue}");
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"Error procesando variable: {varElement}", ex);
                }
            }

            return variables;
        }

        /// <summary>
        /// Normaliza y valida el valor de increment
        /// </summary>
        private string NormalizeIncrementValue(string increment)
        {
            if (string.IsNullOrEmpty(increment))
                return "never";

            var normalized = increment.ToLower().Trim();

            // Valores válidos
            var validIncrements = new Dictionary<string, string>
    {
        { "never", "never" },
        { "per_copy", "per_copy" },
        { "per_item", "per_item" },
        { "per_page", "per_page" },
        { "per_session", "per_session" },
        // Alias comunes
        { "percopy", "per_copy" },
        { "peritem", "per_item" },
        { "perpage", "per_page" },
        { "persession", "per_session" },
        { "copy", "per_copy" },
        { "item", "per_item" },
        { "page", "per_page" },
        { "session", "per_session" }
    };

            if (validIncrements.TryGetValue(normalized, out string validValue))
            {
                return validValue;
            }

            _logger?.LogWarning($"Valor de increment no válido '{increment}'. Usando 'never'.");
            return "never";
        }
        // Helpers para parsing de enums
        private TextAlignment ParseTextAlignment(string align)
        {
            return align?.ToLower() switch
            {
                "center" => TextAlignment.Center,
                "right" => TextAlignment.Right,
                _ => TextAlignment.Left
            };
        }

        private VerticalAlignment ParseVerticalAlignment(string valign)
        {
            return valign?.ToLower() switch
            {
                "middle" => VerticalAlignment.Middle,
                "bottom" => VerticalAlignment.Bottom,
                _ => VerticalAlignment.Top
            };
        }

        private WrapMode ParseWrapMode(string wrap)
        {
            return wrap?.ToLower() switch
            {
                "character" => WrapMode.Character,
                "none" => WrapMode.None,
                _ => WrapMode.Word
            };
        }

        private void ApplyCommonProperties(TemplateObject obj, CommonObjectProperties common)
        {
            obj.X = common.X;
            obj.Y = common.Y;
            obj.Width = common.Width;
            obj.Height = common.Height;
            obj.LockAspectRatio = common.LockAspectRatio;
            obj.Matrix = common.Matrix;
            obj.Shadow = common.Shadow;
        }
    }

    internal class CommonObjectProperties
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public bool LockAspectRatio { get; set; }
        public TransformationMatrix Matrix { get; set; } = new();
        public ShadowEffect? Shadow { get; set; }
    }
}
#pragma warning restore CS8600, CS8601, CS8602, CS8603, CS8604



