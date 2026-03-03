using Microsoft.AspNetCore.Mvc;
using SAELABEL.Api.Contracts;
using SAELABEL.Core.Labels.Servicios;
using SAELABEL.Core.SaeLabels;

namespace SAELABEL.Api.Controllers;

[ApiController]
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

    [HttpPost("parse")]
    public ActionResult<SaeLabelsDocument> Parse([FromBody] XmlPayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.Xml))
        {
            return BadRequest("XML vacío.");
        }

        var doc = SaeLabelsSerializer.Deserialize(payload.Xml);
        return Ok(doc);
    }

    [HttpPost("convert-from-glabels")]
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

    [HttpPost("render")]
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
        var bytes = await _renderer.RenderToImageAsync(glabelTemplate, data, format);

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
}
