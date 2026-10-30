using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OnlineStore.Core.Entities;
using OnlineStore.Core.Interfaces;
using OnlineStore.Infrastructure.Data;
using OnlineStore.Infrastructure.Services;
using Xunit;

namespace OnlineStore.Tests;

public class CatalogTransactionTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private sealed class Source(int id) : IDummyJsonClient
    {
        public Task<IReadOnlyList<SourceProduct>> GetTechnologyProductsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SourceProduct>>([new(id, $"Phone {id}", "Description", "smartphones", null, 10m, "{\"stock\":100}")]);
    }
    private sealed class Rate : IExchangeRateService
    {
        public Task<ExchangeRate> GetAsync(string b, string t, CancellationToken ct = default) => Task.FromResult(new ExchangeRate("USD", "GEL", new(2026, 1, 1), 2.5m));
    }
    private sealed class FailureAfterImport : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data, int result, CancellationToken ct = default)
        {
            if (data.Context!.ChangeTracker.Entries<ProductImport>().Any()) throw new InvalidOperationException("Failure after SQL save before commit.");
            return ValueTask.FromResult(result);
        }
    }

    [Fact]
    public async Task Concurrent_imports_create_one_product_per_external_id()
    {
        async Task<CatalogImportResult> Import()
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<OnlineStoreDbContext>();
            return await new CatalogImportService(db, new(db), new Source(700), new Rate()).SynchronizeAsync();
        }
        var results = await Task.WhenAll(Import(), Import());
        Assert.Equal(1, results.Sum(r => r.Created)); Assert.Equal(1, results.Sum(r => r.Updated));
        await using var verification = factory.Services.CreateAsyncScope();
        var db = verification.ServiceProvider.GetRequiredService<OnlineStoreDbContext>();
        Assert.Single(await db.ProductImports.Where(p => p.ExternalId == 700).ToListAsync());
        Assert.Equal(0, (await db.ProductImports.Include(p => p.Product).SingleAsync(p => p.ExternalId == 700)).Product.StockQuantity);
    }

    [Fact]
    public async Task Failure_after_SQL_insert_rolls_back_product_category_and_metadata()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OnlineStoreDbContext>();
        var beforeProducts = await db.Products.CountAsync(); var beforeCategories = await db.Categories.CountAsync();
        var options = new DbContextOptionsBuilder<OnlineStoreDbContext>().UseSqlServer(factory.ConnectionString).AddInterceptors(new FailureAfterImport()).Options;
        await using (var failing = new OnlineStoreDbContext(options))
            await Assert.ThrowsAsync<InvalidOperationException>(() => new CatalogImportService(failing, new(failing), new Source(701), new Rate()).SynchronizeAsync());
        Assert.Equal(beforeProducts, await db.Products.CountAsync()); Assert.Equal(beforeCategories, await db.Categories.CountAsync());
        Assert.False(await db.ProductImports.AnyAsync(p => p.ExternalId == 701));
    }
}
