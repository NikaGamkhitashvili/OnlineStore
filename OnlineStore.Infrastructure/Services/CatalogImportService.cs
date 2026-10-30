using Microsoft.EntityFrameworkCore;
using OnlineStore.Core.Entities;
using OnlineStore.Core.Exceptions;
using OnlineStore.Core.Interfaces;
using OnlineStore.Infrastructure.Data;

namespace OnlineStore.Infrastructure.Services;

public class CatalogImportService(OnlineStoreDbContext db, StoreTransaction transaction,
    IDummyJsonClient client, IExchangeRateService rates) : ICatalogImportService
{
    public async Task<CatalogImportResult> SynchronizeAsync(CancellationToken ct = default)
    {
        // Network requests finish before taking the store writer lock. Failure makes no database changes.
        var products = await client.GetTechnologyProductsAsync(ct);
        if (products.Count == 0 || products.Any(p => !DummyJsonClient.Categories.ContainsKey(p.Category))
            || products.Select(p => p.Id).Distinct().Count() != products.Count)
            throw new StoreException("The technology import is empty or invalid.", 502);
        var rate = await rates.GetAsync("USD", "GEL", ct);
        if (rate.Rate <= 0 || rate.Rate > 1000) throw new StoreException("The USD/GEL rate is invalid.", 502);
        return await transaction.RunAsync(async () =>
        {
            var existing = await db.ProductImports.ToDictionaryAsync(p => p.ExternalId, ct);
            var categories = await db.Categories.ToListAsync(ct);
            var created = 0;
            foreach (var source in products)
            {
                if (!existing.TryGetValue(source.Id, out var metadata))
                {
                    var categoryName = DummyJsonClient.Categories[source.Category];
                    var category = categories.FirstOrDefault(c => c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase));
                    if (category is null)
                    {
                        category = new Category { Name = categoryName, Description = $"DummyJSON technology catalog: {source.Category}." };
                        categories.Add(category);
                        db.Categories.Add(category);
                    }
                    metadata = new ProductImport
                    {
                        ExternalId = source.Id, InitialUsdToGelRate = rate.Rate, RateDate = rate.Date,
                        Product = new Product
                        {
                            Name = source.Title, Description = source.Description,
                            Price = decimal.Round(source.Price * rate.Rate, 2, MidpointRounding.AwayFromZero),
                            StockQuantity = 0, Category = category
                        }
                    };
                    db.ProductImports.Add(metadata);
                    created++;
                }
                // Never overwrite the store's name, description, category, price or inventory on reimport.
                metadata.Brand = source.Brand;
                metadata.SourceJson = source.Json;
                metadata.LastSyncedAtUtc = DateTime.UtcNow;
            }
            return new CatalogImportResult(created, products.Count - created, existing.Count + created);
        }, ct);
    }
}
