using Microsoft.AspNetCore.Mvc;
using SAELABEL.Api.Contracts;
using SAELABEL.Api.Services;

namespace SAELABEL.Api.Controllers;

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
}

