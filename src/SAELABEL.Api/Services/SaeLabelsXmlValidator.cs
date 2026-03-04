using System.Xml;
using System.Xml.Schema;

namespace SAELABEL.Api.Services;

public sealed class SaeLabelsXmlValidator : ISaeLabelsXmlValidator
{
    private readonly XmlSchemaSet _schemas;

    public SaeLabelsXmlValidator(IWebHostEnvironment env)
    {
        _schemas = new XmlSchemaSet();

        var labelsPath = Path.Combine(env.ContentRootPath, "Schemas", "saelabels.xsd");
        if (File.Exists(labelsPath))
        {
            // El primer parámetro null indica que use el targetNamespace del XSD
            _schemas.Add(null, labelsPath);
        }

        var ticketsPath = Path.Combine(env.ContentRootPath, "Schemas", "saetickets.xsd");
        if (File.Exists(ticketsPath))
        {
            _schemas.Add(null, ticketsPath);
        }

        if (_schemas.Count == 0)
        {
            throw new FileNotFoundException("No se encontraron esquemas XSD en la carpeta Schemas.");
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
            throw new InvalidDataException($"XML SAELABEL inválido: {errors[0]}");
        }
    }
}
