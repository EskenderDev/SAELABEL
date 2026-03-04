using Microsoft.AspNetCore.Mvc;
using SAELABEL.Api.Contracts;
using SAELABEL.Api.Services;
using SAELABEL.Core.Labels.Servicios;
using SAELABEL.Core.Labels.Modelos;

namespace SAELABEL.Api.Controllers;

[ApiController]
[Tags("Labels")]
[Route("api/labels")]
public sealed class LabelsController : ControllerBase
{
    private readonly SaeLabelsTemplateService _glabels;
    private readonly ILabelRenderer _renderer;
    private readonly ISaeLabelsXmlValidator _saeXmlValidator;
    private readonly ILogicalPrinterStore _printerStore;
    private readonly SaeTicketsTemplateService _ticketService;
    private readonly IEditorLibraryStore _libraryStore;

    public LabelsController(
        SaeLabelsTemplateService glabels,
        ILabelRenderer renderer,
        ISaeLabelsXmlValidator saeXmlValidator,
        ILogicalPrinterStore printerStore,
        SaeTicketsTemplateService ticketService,
        IEditorLibraryStore libraryStore)
    {
        _glabels = glabels;
        _renderer = renderer;
        _saeXmlValidator = saeXmlValidator;
        _printerStore = printerStore;
        _ticketService = ticketService;
        _libraryStore = libraryStore;
    }

    [HttpPost("parse", Name = "ParseSaeLabels")]
    public ActionResult<SaeLabelsTemplate> Parse([FromBody] XmlPayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.Xml))
        {
            return BadRequest("XML vacío.");
        }

        try
        {
            _saeXmlValidator.Validate(payload.Xml);
            var doc = _glabels.ParseTemplateXml(payload.Xml);
            return Ok(doc);
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("convert-from-glabels", Name = "ConvertFromGlabels")]
    public ActionResult<string> ConvertFromGlabels([FromBody] XmlPayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.Xml))
        {
            return BadRequest("XML vacío.");
        }

        try
        {
            var template = _glabels.ParseTemplateXml(payload.Xml);
            var xml = SaeLabelsTemplateXmlSerializer.Serialize(template);
            return Ok(xml);
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("convert-to-glabels", Name = "ConvertToGlabels")]
    public ActionResult<string> ConvertToGlabels([FromBody] XmlPayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.Xml))
        {
            return BadRequest("XML vacío.");
        }

        try
        {
            _saeXmlValidator.Validate(payload.Xml);
            var template = _glabels.ParseTemplateXml(payload.Xml);
            var xml = SaeLabelsTemplateXmlSerializer.Serialize(template);
            return Ok(xml);
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("render", Name = "RenderLabelImage")]
    public async Task<IActionResult> Render([FromBody] RenderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Xml))
        {
            return BadRequest("XML vacío.");
        }

        SaeLabelsTemplate glabelTemplate;
        try
        {
            _saeXmlValidator.Validate(request.Xml);
            glabelTemplate = _glabels.ParseTemplateXml(request.Xml);
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(ex.Message);
        }

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

        SaeLabelsTemplate glabelTemplate;
        try
        {
            _saeXmlValidator.Validate(request.Xml);
            glabelTemplate = _glabels.ParseTemplateXml(request.Xml);
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(ex.Message);
        }
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

        if (request.Xml.Contains("<saetickets"))
        {
            try
            {
                _saeXmlValidator.Validate(request.Xml);
                return await PrintTicketInternal(request);
            }
            catch (InvalidDataException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        SaeLabelsTemplate glabelTemplate;
        try
        {
            _saeXmlValidator.Validate(request.Xml);
            glabelTemplate = _glabels.ParseTemplateXml(request.Xml);
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(ex.Message);
        }
        var data = request.Data ?? new Dictionary<string, string>();
        var copies = request.Copies <= 0 ? 1 : request.Copies;

        try
        {
            // Resolve logical printer if exists
            var targetPrinter = request.PrinterName;
            var logicalPrinter = _printerStore.GetById(request.PrinterName) ??
                                 _printerStore.GetAll().FirstOrDefault(p => p.Name.Equals(request.PrinterName, StringComparison.OrdinalIgnoreCase));
            
            if (logicalPrinter != null && logicalPrinter.IsActive)
            {
                targetPrinter = logicalPrinter.PhysicalPrinter;
            }

            bool ok;
            if (request.DataList != null && request.DataList.Count > 0)
            {
                ok = await _renderer.PrintMultipleItemsAsync(glabelTemplate, request.DataList, targetPrinter, copies);
            }
            else
            {
                ok = await _renderer.PrintToPrinterAsync(glabelTemplate, data, targetPrinter, copies);
            }

            if (!ok)
            {
                return StatusCode(StatusCodes.Status502BadGateway, "No se pudo completar la impresión.");
            }
            return Ok(new { printed = true, printer = targetPrinter, originalPrinter = request.PrinterName, copies });
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

        string normalized;
        try
        {
            _saeXmlValidator.Validate(request.Xml);
            // Parse + serialize para validar y normalizar antes de exportar
            var saeDoc = _glabels.ParseTemplateXml(request.Xml);
            normalized = SaeLabelsTemplateXmlSerializer.Serialize(saeDoc);
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(ex.Message);
        }

        var fileName = string.IsNullOrWhiteSpace(request.FileName)
            ? "label.saelabels"
            : request.FileName!.EndsWith(".saelabels", StringComparison.OrdinalIgnoreCase)
                ? request.FileName!
                : $"{request.FileName}.saelabels";

        var bytes = System.Text.Encoding.UTF8.GetBytes(normalized);
        return File(bytes, "application/xml", fileName);
    }
    [HttpGet("library", Name = "GetLabelLibrary")]
    public ActionResult<IEnumerable<EditorDocumentSummaryDto>> GetLibrary()
    {
        return Ok(_libraryStore.GetDocuments());
    }

    [HttpPost("library/{name}/print", Name = "PrintFromLibrary")]
    public async Task<IActionResult> PrintByName(string name, [FromBody] PrintRequest request)
    {
        var doc = _libraryStore.GetDocumentByName(name);
        if (doc == null) return NotFound($"Diseño '{name}' no encontrado.");

        request.Xml = doc.Xml;

        // Resolver impresora lógica → obtener configuración (copias, ancho papel)
        LogicalPrinterDto? logicalPrinter = null;
        if (!string.IsNullOrWhiteSpace(request.PrinterName))
        {
            logicalPrinter = _printerStore.GetById(request.PrinterName)
                          ?? _printerStore.GetByName(request.PrinterName);

            if (logicalPrinter != null)
            {
                // Usar impresora física real
                request.PrinterName = logicalPrinter.IsActive
                    ? logicalPrinter.PhysicalPrinter
                    : request.PrinterName;

                // Aplicar copias del printer si el request no las sobreescribe
                if (request.Copies <= 1) request.Copies = logicalPrinter.Copies;
            }
        }

        // Determinar tipo de documento
        if (doc.Kind == "saetickets" || doc.Xml.Contains("<saetickets"))
        {
            int paperWidth = logicalPrinter?.PaperWidth ?? 0;
            return await PrintTicketInternal(request, paperWidth);
        }

        return await Print(request);
    }

    private async Task<IActionResult> PrintTicketInternal(PrintRequest request, int paperWidth = 0)
    {
        try
        {
            var doc = System.Xml.Linq.XDocument.Parse(request.Xml);
            var setup = doc.Root?.Element("setup");
            var printersAttr = setup?.Attribute("printers")?.Value;
            
            var targetPrinters = new List<string>();
            if (!string.IsNullOrWhiteSpace(printersAttr))
            {
                var names = printersAttr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var name in names)
                {
                    var resolved = ResolvePrinter(name);
                    targetPrinters.Add(resolved);
                }
            }
            else if (!string.IsNullOrWhiteSpace(request.PrinterName))
            {
                targetPrinters.Add(ResolvePrinter(request.PrinterName));
            }

            if (targetPrinters.Count == 0)
                return BadRequest("No se especificó ninguna impresora válida.");

            bool allOk = true;
            int totalSent = 0;

            foreach (var targetPrinter in targetPrinters)
            {
                if (request.DataList is { Count: > 0 } list)
                {
                    var globalData = request.Data ?? new Dictionary<string, string>();
                    foreach (var itemData in list)
                    {
                        // Merge global data into item data (item data wins on conflict)
                        var mergedData = new Dictionary<string, string>(globalData);
                        foreach (var kv in itemData) mergedData[kv.Key] = kv.Value;

                        var bytes = _ticketService.ProcessTicketXml(request.Xml, mergedData, paperWidth);
                        if (!await RawPrintHelper.SendBytesToPrinterAsync(targetPrinter, bytes, "SaeTicket"))
                            allOk = false;
                        totalSent++;
                    }
                }
                else
                {
                    var data = request.Data ?? new Dictionary<string, string>();
                    var bytes = _ticketService.ProcessTicketXml(request.Xml, data, paperWidth);
                    if (!await RawPrintHelper.SendBytesToPrinterAsync(targetPrinter, bytes, "SaeTicket"))
                        allOk = false;
                    totalSent++;
                }
            }

            if (!allOk) return StatusCode(StatusCodes.Status502BadGateway, "Una o más impresiones de tiquetes fallaron.");
            return Ok(new { printed = true, type = "ticket", printers = targetPrinters, totalSent });
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private string ResolvePrinter(string printerName)
    {
        var logicalPrinter = _printerStore.GetById(printerName) ??
                             _printerStore.GetAll().FirstOrDefault(p => p.Name.Equals(printerName, StringComparison.OrdinalIgnoreCase));

        return (logicalPrinter != null && logicalPrinter.IsActive)
            ? logicalPrinter.PhysicalPrinter
            : printerName;
    }
}

// Helper para impresión RAW (Comandos ESC/POS)
internal static class RawPrintHelper
{
    public static Task<bool> SendBytesToPrinterAsync(string printerName, byte[] bytes, string docName)
    {
        try
        {
            return Task.FromResult(SAELABEL.Core.Labels.Helpers.RawPrinterHelper.SendBytesToPrinter(printerName, bytes, docName));
        }
        catch
        {
            return Task.FromResult(false);
        }
    }
}
