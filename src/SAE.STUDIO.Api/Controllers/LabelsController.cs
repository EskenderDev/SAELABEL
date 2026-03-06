using Microsoft.AspNetCore.Mvc;
using SAE.STUDIO.Api.Contracts;
using SAE.STUDIO.Api.Services;
using SAE.STUDIO.Core.Labels.Servicios;
using SAE.STUDIO.Core.Labels.Modelos;

namespace SAE.STUDIO.Api.Controllers;

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
        var copies = (request.Copies ?? 0) <= 0 ? 1 : request.Copies!.Value;

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
                
                // Resolver impresora lógica para obtener sus defaults (ancho, copias)
                var lp = _printerStore.GetById(request.PrinterName) 
                      ?? _printerStore.GetByName(request.PrinterName);
                      
                return await PrintTicketInternal(request, lp);
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
        var requestCopies = request.Copies ?? 0;

        try
        {
            var resolvedPrinters = ResolvePrinters(request.PrinterName);
            if (!resolvedPrinters.Any()) return BadRequest("No se especificaron impresoras válidas.");

            bool allOk = true;
            var targetPrinterNames = new List<string>();

            // Resolver impresora lógica para obtener sus defaults (copias) si aplica
            var lp = _printerStore.GetById(request.PrinterName) 
                  ?? _printerStore.GetByName(request.PrinterName);

            foreach (var rp in resolvedPrinters)
            {
                targetPrinterNames.Add(rp.Name);
                
                // Prioridad Copias (Etiquetas):
                // 1. PhysicalPrinterConfig.Copies (específico por impresora física)
                // 2. requestCopies (override manual del diálogo — si es > 0)
                // 3. logicalPrinter.Copies (default de la impresora lógica general)
                // 4. 1 (mínimo)
                int printerCopies = rp.Copies 
                                 ?? ( (requestCopies > 0) ? requestCopies 
                                    : (lp?.Copies ?? 1) );
                
                // Prioridad Dimensiones Físicas (Etiquetas):
                float? hardwareWidthMm = rp.PaperWidth ?? lp?.PaperWidth;
                float? hardwareHeightMm = rp.PaperHeight ?? lp?.PaperHeight;
                
                bool ok;
                if (request.DataList != null && request.DataList.Count > 0)
                {
                    ok = await _renderer.PrintMultipleItemsAsync(glabelTemplate, request.DataList, rp.Name, printerCopies, hardwareWidthMm, hardwareHeightMm);
                }
                else
                {
                    ok = await _renderer.PrintToPrinterAsync(glabelTemplate, data, rp.Name, printerCopies, hardwareWidthMm, hardwareHeightMm);
                }
                if (!ok) allOk = false;
            }

            if (!allOk)
            {
                return StatusCode(StatusCodes.Status502BadGateway, "Una o más impresiones fallaron.");
            }
            return Ok(new { printed = true, printers = targetPrinterNames, originalPrinter = request.PrinterName, requestCopies });
        }
        catch (PlatformNotSupportedException ex)
        {
            return StatusCode(StatusCodes.Status501NotImplemented, ex.Message);
        }
    }

    [HttpPost("export-SaeLabels", Name = "ExportSaeLabelsFile")]
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
            ? "label.SaeLabels"
            : request.FileName!.EndsWith(".SaeLabels", StringComparison.OrdinalIgnoreCase)
                ? request.FileName!
                : $"{request.FileName}.SaeLabels";

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
                // Copias del printer si no vienen en el request
                if ((request.Copies ?? 0) <= 1) request.Copies = logicalPrinter.Copies;
            }
        }

        // Determinar tipo de documento
        if (doc.Kind == "saetickets" || doc.Xml.Contains("<saetickets"))
        {
            return await PrintTicketInternal(request, logicalPrinter);
        }

        return await Print(request);
    }

    private async Task<IActionResult> PrintTicketInternal(PrintRequest request, LogicalPrinterDto? logicalPrinter = null)
    {
        try
        {
            var doc = System.Xml.Linq.XDocument.Parse(request.Xml);
            var setup = doc.Root?.Element("setup");
            var printersAttr = setup?.Attribute("printers")?.Value;
            
            var resolvedPrinters = new List<PhysicalPrinterConfig>();
            if (!string.IsNullOrWhiteSpace(printersAttr))
            {
                var names = printersAttr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var name in names)
                {
                    resolvedPrinters.AddRange(ResolvePrinters(name));
                }
            }
            else if (!string.IsNullOrWhiteSpace(request.PrinterName))
            {
                resolvedPrinters.AddRange(ResolvePrinters(request.PrinterName));
            }

            if (resolvedPrinters.Count == 0)
                return BadRequest("No se especificó ninguna impresora válida.");

            bool allOk = true;
            int totalSent = 0;

            var failedPrinters = new List<string>();
            var printedNames = new List<string>();
            
            foreach (var rp in resolvedPrinters)
            {
                printedNames.Add(rp.Name);
                
                // Prioridad Ancho:
                // 1. PhysicalPrinterConfig.PaperWidth (específico por impresora)
                // 2. logicalPrinter.PaperWidth (default de la impresora lógica)
                // 3. 0 (cae al ancho definido en el XML)
                int printerWidth = rp.PaperWidth ?? (logicalPrinter?.PaperWidth ?? 0);
                
                // Prioridad Copias:
                // 1. PhysicalPrinterConfig.Copies (específico por impresora)
                // 2. request.Copies (override manual en el diálogo de impresión)
                // 3. logicalPrinter.Copies (default de la impresora lógica)
                // 4. 1 (mínimo absoluto)
                int printerCopies = rp.Copies 
                                 ?? ( (request.Copies ?? 0) > 0 ? request.Copies!.Value 
                                    : (logicalPrinter?.Copies ?? 1) );

                for (int i = 0; i < printerCopies; i++)
                {
                    if (request.DataList is { Count: > 0 } list)
                    {
                        var globalData = request.Data ?? new Dictionary<string, string>();
                        foreach (var itemData in list)
                        {
                            var mergedData = new Dictionary<string, string>(globalData);
                            foreach (var kv in itemData) mergedData[kv.Key] = kv.Value;

                            var bytes = _ticketService.ProcessTicketXml(request.Xml, mergedData, printerWidth);
                            if (!await RawPrintHelper.SendBytesToPrinterAsync(rp.Name, bytes, "SaeTicket"))
                            {
                                allOk = false;
                                if (!failedPrinters.Contains(rp.Name)) failedPrinters.Add(rp.Name);
                            }
                            totalSent++;
                        }
                    }
                    else
                    {
                        var data = request.Data ?? new Dictionary<string, string>();
                        var bytes = _ticketService.ProcessTicketXml(request.Xml, data, printerWidth);
                        if (!await RawPrintHelper.SendBytesToPrinterAsync(rp.Name, bytes, "SaeTicket"))
                        {
                            allOk = false;
                            if (!failedPrinters.Contains(rp.Name)) failedPrinters.Add(rp.Name);
                        }
                        totalSent++;
                    }
                }
            }

            if (!allOk) 
            {
                var msg = $"Fallo al imprimir en: {string.Join(", ", failedPrinters)}.";
                return StatusCode(StatusCodes.Status502BadGateway, msg);
            }
            return Ok(new { printed = true, type = "ticket", printers = printedNames, totalSent });
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private List<PhysicalPrinterConfig> ResolvePrinters(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier)) return new List<PhysicalPrinterConfig>();

        var lp = _printerStore.GetById(identifier) ?? _printerStore.GetByName(identifier);

        if (lp != null && lp.IsActive && lp.Printers?.Count > 0)
        {
            return lp.Printers;
        }
        
        // Si no es una impresora lógica, asumimos que es el nombre de una física
        return new List<PhysicalPrinterConfig> { new PhysicalPrinterConfig { Name = identifier } };
    }
}

// Helper para impresión RAW (Comandos ESC/POS)
internal static class RawPrintHelper
{
    public static Task<bool> SendBytesToPrinterAsync(string printerName, byte[] bytes, string docName)
    {
        try
        {
            return Task.FromResult(SAE.STUDIO.Core.Labels.Helpers.RawPrinterHelper.SendBytesToPrinter(printerName, bytes, docName));
        }
        catch
        {
            return Task.FromResult(false);
        }
    }
}
