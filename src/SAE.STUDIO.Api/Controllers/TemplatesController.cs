using Microsoft.AspNetCore.Mvc;
using SAE.STUDIO.Api.Contracts;
using SAE.STUDIO.Api.Services;

namespace SAE.STUDIO.Api.Controllers;

[ApiController]
[Tags("Templates")]
[Route("api/templates")]
public sealed class TemplatesController : ControllerBase
{
    private readonly IEditorLibraryStore _libraryStore;

    public TemplatesController(IEditorLibraryStore libraryStore)
    {
        _libraryStore = libraryStore;
    }

    [HttpGet(Name = "GetTemplates")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public ActionResult<IEnumerable<EditorTemplateDto>> GetTemplates()
    {
        return Ok(_libraryStore.GetTemplates());
    }

    [HttpPost(Name = "UpsertTemplate")]
    public ActionResult<EditorTemplateDto> UpsertTemplate([FromBody] UpsertEditorTemplateRequest request)
    {
        try
        {
            var template = _libraryStore.UpsertTemplate(request);
            return Ok(template);
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
