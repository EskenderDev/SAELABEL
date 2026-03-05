using SAE.STUDIO.Core.Labels.Caching;
using SAE.STUDIO.Core.Labels.Servicios;
using SAE.STUDIO.Api.Services;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.OpenApi;
using System.Net;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

builder.WebHost.UseUrls("http://localhost:5117");
builder.Host.UseWindowsService(options =>
{
    options.ServiceName = "SAE.STUDIO.Api";
});

builder.Services.AddControllers();
var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendClients", policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
            {
                if (string.IsNullOrWhiteSpace(origin))
                {
                    return false;
                }

                if (configuredOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                {
                    return false;
                }

                var isLocalhost = uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                                  uri.Host.Equals("tauri.localhost", StringComparison.OrdinalIgnoreCase) ||
                                  uri.Host.Equals(IPAddress.Loopback.ToString(), StringComparison.OrdinalIgnoreCase);

                var isHttpLocal = (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) && isLocalhost;
                var isTauri = uri.Scheme.Equals("tauri", StringComparison.OrdinalIgnoreCase);

                return isHttpLocal || isTauri;
            })
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddOpenApi("v1", options =>
{
    options.AddOperationTransformer((operation, context, _) =>
    {
        var path = context.Description.RelativePath?.ToLowerInvariant() ?? string.Empty;
        var method = context.Description.HttpMethod?.ToUpperInvariant() ?? string.Empty;

        if (method != "POST" || !path.StartsWith("api/labels/"))
        {
            return Task.CompletedTask;
        }

        if (path == "api/labels/parse")
        {
            operation.Summary = "Parsea un documento .SaeLabels";
            operation.Description = """
                Recibe XML SAE.STUDIO y responde el documento estructurado en JSON.

                **Request example**
                ```json
                { "xml": "<SaeLabels version=\"1.0\"><template brand=\"SAE\" description=\"Demo\" part=\"P-1\" size=\"custom\"><label_rectangle width_pt=\"144\" height_pt=\"72\" round_pt=\"0\" x_waste_pt=\"0\" y_waste_pt=\"0\" /><layout dx_pt=\"0\" dy_pt=\"0\" nx=\"1\" ny=\"1\" x0_pt=\"0\" y0_pt=\"0\" /></template><objects /><variables /></SaeLabels>" }
                ```
                """;
        }
        else if (path == "api/labels/convert-from-glabels")
        {
            operation.Summary = "Convierte XML glabels a .SaeLabels";
            operation.Description = """
                Toma un XML con estructura glabels y devuelve XML SAE.STUDIO normalizado.

                **Request example**
                ```json
                { "xml": "<Glabels-document><Template brand=\"Avery\" description=\"Mailing\" part=\"8160\" size=\"US-Letter\"><Label-rectangle width=\"189pt\" height=\"72pt\" round=\"0pt\"><Layout dx=\"200pt\" dy=\"72pt\" nx=\"3\" ny=\"10\" x0=\"11.25pt\" y0=\"36pt\" /></Label-rectangle></Template><Objects /><Variables /></Glabels-document>" }
                ```
                """;
        }
        else if (path == "api/labels/convert-to-glabels")
        {
            operation.Summary = "Convierte .SaeLabels a XML glabels";
            operation.Description = """
                Toma XML SAE.STUDIO y devuelve XML compatible glabels.

                **Request example**
                ```json
                { "xml": "<SaeLabels version=\"1.0\"><template brand=\"SAE\" description=\"Demo\" part=\"P-1\" size=\"custom\"><label_rectangle width_pt=\"144\" height_pt=\"72\" round_pt=\"0\" x_waste_pt=\"0\" y_waste_pt=\"0\" /><layout dx_pt=\"0\" dy_pt=\"0\" nx=\"1\" ny=\"1\" x0_pt=\"0\" y0_pt=\"0\" /></template><objects /><variables /></SaeLabels>" }
                ```
                """;
        }
        else if (path == "api/labels/render")
        {
            operation.Summary = "Renderiza etiqueta a imagen";
            operation.Description = """
                Renderiza una etiqueta SAE.STUDIO y devuelve archivo de imagen.

                **Request example**
                ```json
                {
                  "xml": "<SaeLabels version=\"1.0\">...</SaeLabels>",
                  "format": "png",
                  "data": { "SKU": "ABC-123" }
                }
                ```
                """;
        }
        else if (path == "api/labels/zpl")
        {
            operation.Summary = "Genera ZPL desde .SaeLabels";
            operation.Description = """
                Genera archivo `.zpl` con soporte de copias y variables.

                **Request example**
                ```json
                {
                  "xml": "<SaeLabels version=\"1.0\">...</SaeLabels>",
                  "copies": 2,
                  "data": { "SKU": "ABC-123" }
                }
                ```
                """;
        }
        else if (path == "api/labels/print")
        {
            operation.Summary = "Imprime etiqueta";
            operation.Description = """
                Envía la etiqueta a una impresora destino.

                **Request example**
                ```json
                {
                  "xml": "<SaeLabels version=\"1.0\">...</SaeLabels>",
                  "printerName": "Zebra_ZD420",
                  "copies": 1,
                  "data": { "SKU": "ABC-123" }
                }
                ```
                """;
        }
        else if (path == "api/labels/export-SaeLabels")
        {
            operation.Summary = "Exporta archivo .SaeLabels";
            operation.Description = """
                Valida y devuelve descarga XML con extensión `.SaeLabels`.

                **Request example**
                ```json
                {
                  "xml": "<SaeLabels version=\"1.0\">...</SaeLabels>",
                  "fileName": "producto-etiqueta"
                }
                ```
                """;
        }

        return Task.CompletedTask;
    });
});

builder.Services.AddSingleton<TemplateCache>();
builder.Services.AddScoped<PrinterOptimizer>();
if (OperatingSystem.IsWindows())
{
    builder.Services.AddScoped<ILabelRenderer, LabelRenderer>();
}
else
{
    builder.Services.AddScoped<ILabelRenderer, UnsupportedLabelRenderer>();
}
builder.Services.AddScoped<SaeLabelsTemplateService>();
builder.Services.AddScoped<SaeTicketsTemplateService>();
builder.Services.AddSingleton<ISaeLabelsXmlValidator, SaeLabelsXmlValidator>();
builder.Services.AddSingleton<IEditorLibraryStore, EditorLibraryStore>();
builder.Services.AddSingleton<ILogicalPrinterStore, LogicalPrinterStore>();

var app = builder.Build();

app.MapOpenApi("/openapi/{documentName}.json");
app.MapScalarApiReference("/scalar", options =>
{
    options
        .WithTitle("SAE.STUDIO API")
        .WithTheme(ScalarTheme.DeepSpace)
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
});

// CORS must come BEFORE UseHttpsRedirection — the redirect 301 doesn't carry CORS headers
// and the browser blocks the preflight before our policy ever runs.
app.UseCors("FrontendClients");
app.UseHttpsRedirection();
app.MapControllers();
app.Run();

internal sealed class UnsupportedLabelRenderer : ILabelRenderer
{
    private static Exception NotSupported() =>
        new PlatformNotSupportedException("ILabelRenderer requiere Windows por dependencias System.Drawing.");

    public string GenerateZpl(SAE.STUDIO.Core.Labels.Modelos.SaeLabelsTemplate template, Dictionary<string, string> data)
        => throw NotSupported();

    public Task<string> GenerateZplWithCopiesAsync(SAE.STUDIO.Core.Labels.Modelos.SaeLabelsTemplate template, Dictionary<string, string> data, int copies = 1)
        => throw NotSupported();

    public Task<bool> PrintToPrinterAsync(SAE.STUDIO.Core.Labels.Modelos.SaeLabelsTemplate template, Dictionary<string, string> data, string printerName, int copies = 1)
        => throw NotSupported();

    public Task<bool> PrintMultipleItemsAsync(SAE.STUDIO.Core.Labels.Modelos.SaeLabelsTemplate template, IEnumerable<Dictionary<string, string>> itemsData, string printerName, int copiesPerItem = 1)
        => throw NotSupported();

    public Task<byte[]> RenderToImageAsync(SAE.STUDIO.Core.Labels.Modelos.SaeLabelsTemplate template, Dictionary<string, string> data, string format = "png")
        => throw NotSupported();

    public System.Drawing.Bitmap RenderToBitmap(SAE.STUDIO.Core.Labels.Modelos.SaeLabelsTemplate template, Dictionary<string, string> data, SAE.STUDIO.Core.Labels.Servicios.RenderSettings? settings = null)
        => throw NotSupported();
        
    public IEnumerable<string> GetInstalledPrinters()
        => throw NotSupported();
}
