using System.Xml;
using System.Xml.Schema;

namespace SAELABEL.Api.Services;

public sealed class GlabelsXmlValidator : IGlabelsXmlValidator
{
    private readonly XmlSchemaSet _schemas;

    public GlabelsXmlValidator(IWebHostEnvironment env)
    {
        var schemaPath = Path.Combine(env.ContentRootPath, "Schemas", "glabels.xsd");
        if (!File.Exists(schemaPath))
        {
            throw new FileNotFoundException($"No se encontró esquema XSD: {schemaPath}");
        }

        _schemas = new XmlSchemaSet();
        _schemas.Add(null, schemaPath);
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
            throw new InvalidDataException($"XML glabels inválido: {errors[0]}");
        }
    }
}
