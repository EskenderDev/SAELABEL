using Microsoft.AspNetCore.Mvc;
using SAELABEL.Api.Contracts;
using SAELABEL.Core.Labels.Servicios;
using SAELABEL.Core.SaeLabels;

namespace SAELABEL.Api.Controllers;

[ApiController]
[Tags("Labels")]
[Route("api/labels")]
public sealed class LabelsController : ControllerBase
{
    private readonly GlabelsTemplateService _glabels;
    private readonly ILabelRenderer _renderer;

    public LabelsController(GlabelsTemplateService glabels, ILabelRenderer renderer)
    {
        _glabels = glabels;
        _renderer = renderer;
    }

    [HttpPost("parse", Name = "ParseSaeLabels")]
    public ActionResult<SaeLabelsDocument> Parse([FromBody] XmlPayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.Xml))
        {
            return BadRequest("XML vacío.");
        }

        var doc = SaeLabelsSerializer.Deserialize(payload.Xml);
        return Ok(doc);
    }

    [HttpPost("convert-from-glabels", Name = "ConvertFromGlabels")]
    public ActionResult<string> ConvertFromGlabels([FromBody] XmlPayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.Xml))
        {
            return BadRequest("XML vacío.");
        }

        var template = _glabels.ParseTemplateXml(payload.Xml);
        var saeDoc = SaeLabelsConverter.FromGlabelsTemplate(template);
        var xml = SaeLabelsSerializer.Serialize(saeDoc);
        return Ok(xml);
    }

    [HttpPost("convert-to-glabels", Name = "ConvertToGlabels")]
    public ActionResult<string> ConvertToGlabels([FromBody] XmlPayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.Xml))
        {
            return BadRequest("XML vacío.");
        }

        var saeDoc = SaeLabelsSerializer.Deserialize(payload.Xml);
        var template = SaeLabelsConverter.ToGlabelsTemplate(saeDoc);
        var xml = GlabelsTemplateXmlSerializer.Serialize(template);
        return Ok(xml);
    }

    [HttpPost("render", Name = "RenderLabelImage")]
    public async Task<IActionResult> Render([FromBody] RenderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Xml))
        {
            return BadRequest("XML vacío.");
        }

        var saeDoc = SaeLabelsSerializer.Deserialize(request.Xml);
        var glabelTemplate = SaeLabelsConverter.ToGlabelsTemplate(saeDoc);

        var format = string.IsNullOrWhiteSpace(request.Format) ? "png" : request.Format.ToLowerInvariant();
        if (format is not ("png" or "jpeg" or "jpg" or "bmp" or "gif" or "tiff"))
        {
            return BadRequest("Formato no soportado. Use: png, jpeg, jpg, bmp, gif, tiff.");
        }

        var data = request.Data ?? new Dictionary<string, string>();
        byte[] bytes;
        try
        {
            bytes = await _renderer.RenderToImageAsync(glabelTemplate, data, format);
        }
        catch (PlatformNotSupportedException ex)
        {
            return StatusCode(StatusCodes.Status501NotImplemented, ex.Message);
        }

        var contentType = format switch
        {
            "jpg" => "image/jpeg",
            "jpeg" => "image/jpeg",
            "bmp" => "image/bmp",
            "gif" => "image/gif",
            "tiff" => "image/tiff",
            _ => "image/png"
        };

        var extension = format == "jpg" ? "jpeg" : format;
        return File(bytes, contentType, $"label.{extension}");
    }

    [HttpPost("zpl", Name = "GenerateLabelZpl")]
    public async Task<IActionResult> GenerateZpl([FromBody] ZplRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Xml))
        {
            return BadRequest("XML vacío.");
        }

        var saeDoc = SaeLabelsSerializer.Deserialize(request.Xml);
        var glabelTemplate = SaeLabelsConverter.ToGlabelsTemplate(saeDoc);
        var data = request.Data ?? new Dictionary<string, string>();
        var copies = request.Copies <= 0 ? 1 : request.Copies;

        try
        {
            var zpl = await _renderer.GenerateZplWithCopiesAsync(glabelTemplate, data, copies);
            var bytes = System.Text.Encoding.UTF8.GetBytes(zpl);
            return File(bytes, "text/plain", "label.zpl");
        }
        catch (PlatformNotSupportedException ex)
        {
            return StatusCode(StatusCodes.Status501NotImplemented, ex.Message);
        }
    }

    [HttpPost("print", Name = "PrintLabel")]
    public async Task<IActionResult> Print([FromBody] PrintRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Xml))
        {
            return BadRequest("XML vacío.");
        }
        if (string.IsNullOrWhiteSpace(request.PrinterName))
        {
            return BadRequest("PrinterName es requerido.");
        }

        var saeDoc = SaeLabelsSerializer.Deserialize(request.Xml);
        var glabelTemplate = SaeLabelsConverter.ToGlabelsTemplate(saeDoc);
        var data = request.Data ?? new Dictionary<string, string>();
        var copies = request.Copies <= 0 ? 1 : request.Copies;

        try
        {
            var ok = await _renderer.PrintToPrinterAsync(glabelTemplate, data, request.PrinterName, copies);
            if (!ok)
            {
                return StatusCode(StatusCodes.Status502BadGateway, "No se pudo completar la impresión.");
            }
            return Ok(new { printed = true, printer = request.PrinterName, copies });
        }
        catch (PlatformNotSupportedException ex)
        {
            return StatusCode(StatusCodes.Status501NotImplemented, ex.Message);
        }
    }

    [HttpPost("export-saelabels", Name = "ExportSaeLabelsFile")]
    public IActionResult ExportSaeLabels([FromBody] ExportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Xml))
        {
            return BadRequest("XML vacío.");
        }

        // Parse + serialize para validar y normalizar antes de exportar
        var saeDoc = SaeLabelsSerializer.Deserialize(request.Xml);
        var normalized = SaeLabelsSerializer.Serialize(saeDoc);

        var fileName = string.IsNullOrWhiteSpace(request.FileName)
            ? "label.saelabels"
            : request.FileName!.EndsWith(".saelabels", StringComparison.OrdinalIgnoreCase)
                ? request.FileName!
                : $"{request.FileName}.saelabels";

        var bytes = System.Text.Encoding.UTF8.GetBytes(normalized);
        return File(bytes, "application/xml", fileName);
    }
}
