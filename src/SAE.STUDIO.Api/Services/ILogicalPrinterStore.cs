using SAE.STUDIO.Api.Contracts;

namespace SAE.STUDIO.Api.Services;

public interface ILogicalPrinterStore
{
    IReadOnlyList<LogicalPrinterDto> GetAll();
    LogicalPrinterDto? GetById(string id);
    LogicalPrinterDto? GetByName(string name);
    LogicalPrinterDto Upsert(UpsertLogicalPrinterRequest request);
    bool Delete(string id);
}
