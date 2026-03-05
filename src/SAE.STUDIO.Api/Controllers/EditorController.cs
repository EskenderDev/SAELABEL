using Microsoft.AspNetCore.Mvc;
using SAE.STUDIO.Api.Contracts;
using SAE.STUDIO.Api.Services;

namespace SAE.STUDIO.Api.Controllers;

[ApiController]
[Tags("Editor")]
[Route("api/editor")]
public sealed class EditorController : ControllerBase
{
    private readonly IEditorLibraryStore _store;

    public EditorController(IEditorLibraryStore store)
    {
        _store = store;
    }

    [HttpGet("elements", Name = "GetEditorElements")]
    public ActionResult<IReadOnlyList<EditorElementDto>> GetElements()
    {
        return Ok(_store.GetElements());
    }

    [HttpPost("elements", Name = "UpsertEditorElement")]
    public ActionResult<EditorElementDto> UpsertElement([FromBody] UpsertEditorElementRequest request)
    {
        try
        {
            var element = _store.UpsertElement(request);
            return Ok(element);
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("elements/{id}", Name = "DeleteEditorElement")]
    public IActionResult DeleteElement([FromRoute] string id)
    {
        if (!_store.DeleteElement(id))
        {
            return NotFound();
        }
        return NoContent();
    }

    [HttpGet("documents", Name = "GetEditorDocuments")]
    public ActionResult<IReadOnlyList<EditorDocumentSummaryDto>> GetDocuments()
    {
        return Ok(_store.GetDocuments());
    }

    [HttpGet("documents/{id}", Name = "GetEditorDocument")]
    public ActionResult<EditorDocumentDto> GetDocument([FromRoute] string id)
    {
        var document = _store.GetDocument(id);
        if (document is null)
        {
            return NotFound();
        }
        return Ok(document);
    }

    [HttpGet("documents/by-name/{name}", Name = "GetEditorDocumentByName")]
    public ActionResult<EditorDocumentDto> GetDocumentByName([FromRoute] string name)
    {
        var document = _store.GetDocumentByName(name);
        if (document is null)
        {
            return NotFound();
        }
        return Ok(document);
    }

    [HttpPost("documents", Name = "UpsertEditorDocument")]
    public ActionResult<EditorDocumentDto> UpsertDocument([FromBody] UpsertEditorDocumentRequest request)
    {
        try
        {
            var document = _store.UpsertDocument(request);
            return Ok(document);
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("documents/{id}", Name = "DeleteEditorDocument")]
    public IActionResult DeleteDocument([FromRoute] string id)
    {
        if (!_store.DeleteDocument(id))
        {
            return NotFound();
        }
        return NoContent();
    }

    [HttpGet("settings/{key}", Name = "GetEditorSetting")]
    public ActionResult<EditorSettingDto> GetSetting([FromRoute] string key)
    {
        var val = _store.GetSetting(key);
        return Ok(new EditorSettingDto { Key = key, Value = val ?? string.Empty });
    }

    [HttpPost("settings", Name = "SaveEditorSetting")]
    public IActionResult SaveSetting([FromBody] UpdateEditorSettingRequest request)
    {
        _store.SaveSetting(request.Key, request.Value);
        return NoContent();
    }

    [HttpPost("export/saesystem", Name = "ExportToSaeSystem")]
    public IActionResult ExportToSaeSystem([FromBody] ExportRequest request)
    {
        var path = _store.GetSetting("saesystem_path");
        if (string.IsNullOrWhiteSpace(path))
        {
            return BadRequest("Ruta de exportacion SAE System no configurada.");
        }

        try
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            var fileName = string.IsNullOrWhiteSpace(request.FileName) 
                ? $"export_{DateTime.Now:yyyyMMdd_HHmmss}.xml" 
                : request.FileName;
            
            if (!fileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            {
                fileName += ".xml";
            }

            var fullPath = Path.Combine(path, fileName);
            System.IO.File.WriteAllText(fullPath, request.Xml);
            
            return Ok(new { Message = "Exportado con éxito", Path = fullPath });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error al exportar: {ex.Message}");
        }
    }
}

