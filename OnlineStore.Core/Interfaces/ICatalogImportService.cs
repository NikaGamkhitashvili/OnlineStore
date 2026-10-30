namespace OnlineStore.Core.Interfaces;

public record SourceProduct(int Id, string Title, string Description, string Category, string? Brand, decimal Price, string Json);
public record CatalogImportResult(int Created, int Updated, int ImportedTotal);

public interface IDummyJsonClient
{
    Task<IReadOnlyList<SourceProduct>> GetTechnologyProductsAsync(CancellationToken ct = default);
}

public interface ICatalogImportService
{
    Task<CatalogImportResult> SynchronizeAsync(CancellationToken ct = default);
}
