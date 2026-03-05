using System.Xml;
using System.Xml.Schema;

namespace SAE.STUDIO.Api.Services;

public sealed class SaeLabelsXmlValidator : ISaeLabelsXmlValidator
{
    private readonly XmlSchemaSet _schemas;

    public SaeLabelsXmlValidator(IWebHostEnvironment env)
    {
        _schemas = new XmlSchemaSet();

        // Resolve the Schemas directory from multiple candidate paths.
        // When published as a self-contained single-file and running as a Windows
        // service the working directory and AppContext.BaseDirectory may differ from
        // the actual install location, so we try the executable's own directory first.
        var candidateBases = new[]
        {
            Path.GetDirectoryName(Environment.ProcessPath) ?? string.Empty,
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory(),
        };

        string? schemasDir = candidateBases
            .Select(b => Path.Combine(b, "Schemas"))
            .FirstOrDefault(Directory.Exists);

        if (schemasDir is null)
        {
            throw new FileNotFoundException(
                "No se encontraron esquemas XSD en la carpeta Schemas. " +
                "Buscado en: " + string.Join(", ", candidateBases.Select(b => Path.Combine(b, "Schemas"))));
        }

        var labelsPath = Path.Combine(schemasDir, "SaeLabels.xsd");
        if (File.Exists(labelsPath))
            _schemas.Add(null, labelsPath);

        var ticketsPath = Path.Combine(schemasDir, "saetickets.xsd");
        if (File.Exists(ticketsPath))
            _schemas.Add(null, ticketsPath);

        if (_schemas.Count == 0)
        {
            throw new FileNotFoundException($"Se encontró la carpeta Schemas en '{schemasDir}' pero no contiene archivos XSD válidos (SaeLabels.xsd / saetickets.xsd).");
        }
    }

    public void Validate(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            throw new InvalidDataException("XML vacío.");
        }

        var errors = new List<string>();
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            ValidationType = ValidationType.Schema,
            Schemas = _schemas
        };
        settings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
        settings.ValidationEventHandler += (_, e) =>
        {
            var message = e.Exception is null
                ? e.Message
                : $"Línea {e.Exception.LineNumber}, pos {e.Exception.LinePosition}: {e.Exception.Message}";
            errors.Add(message);
        };

        try
        {
            using var sr = new StringReader(xml);
            using var reader = XmlReader.Create(sr, settings);
            while (reader.Read()) { }
        }
        catch (XmlException ex)
        {
            throw new InvalidDataException($"XML inválido: línea {ex.LineNumber}, pos {ex.LinePosition}. {ex.Message}");
        }

        if (errors.Count > 0)
        {
            throw new InvalidDataException($"XML SAE.STUDIO inválido: {errors[0]}");
        }
    }
}
