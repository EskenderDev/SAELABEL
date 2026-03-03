using SAELABEL.Core.Labels.Caching;
using SAELABEL.Core.Labels.Servicios;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

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
