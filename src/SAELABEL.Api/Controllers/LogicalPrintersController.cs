using Microsoft.AspNetCore.Mvc;
using SAELABEL.Api.Contracts;
using SAELABEL.Api.Services;
using SAELABEL.Core.Labels.Servicios;

namespace SAELABEL.Api.Controllers;

[ApiController]
[Tags("Logical Printers")]
[Route("api/logical-printers")]
public sealed class LogicalPrintersController : ControllerBase
{
    private readonly ILogicalPrinterStore _store;
    private readonly ILabelRenderer _renderer;

    public LogicalPrintersController(ILogicalPrinterStore store, ILabelRenderer renderer)
    {
        _store = store;
        _renderer = renderer;
    }

    [HttpGet("system-printers", Name = "GetSystemPrinters")]
    public ActionResult<IEnumerable<string>> GetSystemPrinters()
    {
        try
        {
            var printers = _renderer.GetInstalledPrinters();
            return Ok(printers);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error obteniendo impresoras del sistema: {ex.Message}");
        }
    }

    [HttpGet(Name = "GetAllLogicalPrinters")]
    public ActionResult<IReadOnlyList<LogicalPrinterDto>> GetAll()
    {
        return Ok(_store.GetAll());
    }

    [HttpGet("{id}", Name = "GetLogicalPrinterById")]
    public ActionResult<LogicalPrinterDto> GetById([FromRoute] string id)
    {
        var printer = _store.GetById(id);
        if (printer is null)
        {
            return NotFound();
        }
        return Ok(printer);
    }

    [HttpPost(Name = "UpsertLogicalPrinter")]
    public ActionResult<LogicalPrinterDto> Upsert([FromBody] UpsertLogicalPrinterRequest request)
    {
        try
        {
            var printer = _store.Upsert(request);
            return Ok(printer);
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{id}", Name = "DeleteLogicalPrinter")]
    public IActionResult Delete([FromRoute] string id)
    {
        if (!_store.Delete(id))
        {
            return NotFound();
        }
        return NoContent();
    }
}
