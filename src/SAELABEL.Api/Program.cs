using SAELABEL.Core.Labels.Caching;
using SAELABEL.Core.Labels.Servicios;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
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
            operation.Summary = "Parsea un documento .saelabels";
            operation.Description = """
                Recibe XML SAELABEL y responde el documento estructurado en JSON.

                **Request example**
                ```json
                { "xml": "<saelabels version=\"1.0\"><template brand=\"SAE\" description=\"Demo\" part=\"P-1\" size=\"custom\"><label_rectangle width_pt=\"144\" height_pt=\"72\" round_pt=\"0\" x_waste_pt=\"0\" y_waste_pt=\"0\" /><layout dx_pt=\"0\" dy_pt=\"0\" nx=\"1\" ny=\"1\" x0_pt=\"0\" y0_pt=\"0\" /></template><objects /><variables /></saelabels>" }
                ```
                """;
        }
        else if (path == "api/labels/convert-from-glabels")
        {
            operation.Summary = "Convierte XML glabels a .saelabels";
            operation.Description = """
                Toma un XML con estructura glabels y devuelve XML SAELABEL normalizado.

                **Request example**
                ```json
                { "xml": "<Glabels-document><Template brand=\"Avery\" description=\"Mailing\" part=\"8160\" size=\"US-Letter\"><Label-rectangle width=\"189pt\" height=\"72pt\" round=\"0pt\"><Layout dx=\"200pt\" dy=\"72pt\" nx=\"3\" ny=\"10\" x0=\"11.25pt\" y0=\"36pt\" /></Label-rectangle></Template><Objects /><Variables /></Glabels-document>" }
                ```
                """;
        }
        else if (path == "api/labels/convert-to-glabels")
        {
            operation.Summary = "Convierte .saelabels a XML glabels";
            operation.Description = """
                Toma XML SAELABEL y devuelve XML compatible glabels.

                **Request example**
                ```json
                { "xml": "<saelabels version=\"1.0\"><template brand=\"SAE\" description=\"Demo\" part=\"P-1\" size=\"custom\"><label_rectangle width_pt=\"144\" height_pt=\"72\" round_pt=\"0\" x_waste_pt=\"0\" y_waste_pt=\"0\" /><layout dx_pt=\"0\" dy_pt=\"0\" nx=\"1\" ny=\"1\" x0_pt=\"0\" y0_pt=\"0\" /></template><objects /><variables /></saelabels>" }
                ```
                """;
        }
        else if (path == "api/labels/render")
        {
            operation.Summary = "Renderiza etiqueta a imagen";
            operation.Description = """
                Renderiza una etiqueta SAELABEL y devuelve archivo de imagen.

                **Request example**
                ```json
                {
                  "xml": "<saelabels version=\"1.0\">...</saelabels>",
                  "format": "png",
                  "data": { "SKU": "ABC-123" }
                }
                ```
                """;
        }
        else if (path == "api/labels/zpl")
        {
            operation.Summary = "Genera ZPL desde .saelabels";
            operation.Description = """
                Genera archivo `.zpl` con soporte de copias y variables.

                **Request example**
                ```json
                {
                  "xml": "<saelabels version=\"1.0\">...</saelabels>",
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
                  "xml": "<saelabels version=\"1.0\">...</saelabels>",
                  "printerName": "Zebra_ZD420",
                  "copies": 1,
                  "data": { "SKU": "ABC-123" }
                }
                ```
                """;
        }
        else if (path == "api/labels/export-saelabels")
        {
            operation.Summary = "Exporta archivo .saelabels";
            operation.Description = """
                Valida y devuelve descarga XML con extensión `.saelabels`.

                **Request example**
                ```json
                {
                  "xml": "<saelabels version=\"1.0\">...</saelabels>",
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
builder.Services.AddScoped<GlabelsTemplateService>();

var app = builder.Build();

app.MapOpenApi("/openapi/{documentName}.json");
app.MapScalarApiReference("/scalar", options =>
{
    options
        .WithTitle("SAELABEL API")
        .WithTheme(ScalarTheme.DeepSpace)
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
});

app.UseHttpsRedirection();
app.MapControllers();
app.Run();

internal sealed class UnsupportedLabelRenderer : ILabelRenderer
{
    private static Exception NotSupported() =>
        new PlatformNotSupportedException("ILabelRenderer requiere Windows por dependencias System.Drawing.");

    public string GenerateZpl(SAELABEL.Core.Labels.Modelos.GlabelsTemplate template, Dictionary<string, string> data)
        => throw NotSupported();

    public Task<string> GenerateZplWithCopiesAsync(SAELABEL.Core.Labels.Modelos.GlabelsTemplate template, Dictionary<string, string> data, int copies = 1)
        => throw NotSupported();

    public Task<bool> PrintToPrinterAsync(SAELABEL.Core.Labels.Modelos.GlabelsTemplate template, Dictionary<string, string> data, string printerName, int copies = 1)
        => throw NotSupported();

    public Task<bool> PrintMultipleItemsAsync(SAELABEL.Core.Labels.Modelos.GlabelsTemplate template, IEnumerable<Dictionary<string, string>> itemsData, string printerName, int copiesPerItem = 1)
        => throw NotSupported();

    public Task<byte[]> RenderToImageAsync(SAELABEL.Core.Labels.Modelos.GlabelsTemplate template, Dictionary<string, string> data, string format = "png")
        => throw NotSupported();

    public System.Drawing.Bitmap RenderToBitmap(SAELABEL.Core.Labels.Modelos.GlabelsTemplate template, Dictionary<string, string> data, SAELABEL.Core.Labels.Servicios.RenderSettings? settings = null)
        => throw NotSupported();
}
