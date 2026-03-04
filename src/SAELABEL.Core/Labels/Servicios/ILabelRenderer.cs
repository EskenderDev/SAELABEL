using SAELABEL.Core.Labels.Modelos;
using System.Drawing;

namespace SAELABEL.Core.Labels.Servicios
{
    public interface ILabelRenderer
    {
        // Métodos de generación
        string GenerateZpl(SaeLabelsTemplate template, Dictionary<string, string> data);
        Task<string> GenerateZplWithCopiesAsync(SaeLabelsTemplate template, Dictionary<string, string> data, int copies = 1);

        // Métodos de impresión
        Task<bool> PrintToPrinterAsync(SaeLabelsTemplate template, Dictionary<string, string> data, string printerName, int copies = 1);

        Task<bool> PrintMultipleItemsAsync(
            SaeLabelsTemplate template,
            IEnumerable<Dictionary<string, string>> itemsData,
            string printerName,
            int copiesPerItem = 1);

        // Métodos de renderizado visual
        Task<byte[]> RenderToImageAsync(SaeLabelsTemplate template, Dictionary<string, string> data, string format = "png");
        Bitmap RenderToBitmap(SaeLabelsTemplate template, Dictionary<string, string> data, RenderSettings? settings = null);

        // System printers
        IEnumerable<string> GetInstalledPrinters();
    }
}
