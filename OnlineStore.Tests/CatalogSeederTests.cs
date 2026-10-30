using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OnlineStore.API.DTOs;
using OnlineStore.Core.Entities;
using OnlineStore.Infrastructure.Data;
using Xunit;

namespace OnlineStore.Tests;

public class CatalogSeederTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Catalog_fills_to_30_preserves_existing_records_and_restarts_without_duplicates()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OnlineStoreDbContext>();
        await scope.ServiceProvider.GetRequiredService<DevelopmentSeeder>().SeedAsync();
        var qaCategory = new Category { Name = "UI QA აქსესუარები", Description = "Existing category" };
        var qaMouse = new Product { Name = "UI QA Mouse", Description = "Existing description", Price = 99m, StockQuantity = 5, Category = qaCategory };
        var qaOrder = new Order
        {
            CustomerId = await db.Customers.Select(c => c.Id).FirstAsync(),
            OrderDate = DateTime.UtcNow, Status = "Cancelled", TotalAmount = 24.70m,
            Items = new List<OrderItem> { new() { Product = qaMouse, Quantity = 2, UnitPrice = 12.35m } }
        };
        db.Orders.Add(qaOrder);
        await db.SaveChangesAsync();
        var originalIds = await db.Products.Select(p => p.Id).ToListAsync();
        var productBefore = await ProductSnapshot(db, originalIds);
        var customersBefore = await CustomerSnapshot(db);
        var ordersBefore = await OrderSnapshot(db);
        var itemsBefore = await ItemSnapshot(db);
        var oldCategories = await db.Categories.AsNoTracking().Select(c => new { c.Id, c.Name, c.Description }).ToListAsync();

        await scope.ServiceProvider.GetRequiredService<CatalogSeeder>().SeedAsync();
        Assert.Equal(30, await db.Products.CountAsync());
        Assert.Equal(9, await db.Categories.CountAsync());
        Assert.Equal(productBefore, await ProductSnapshot(db, originalIds));
        Assert.Equal(customersBefore, await CustomerSnapshot(db));
        Assert.Equal(ordersBefore, await OrderSnapshot(db));
        Assert.Equal(itemsBefore, await ItemSnapshot(db));
        foreach (var category in oldCategories)
        {
            var saved = await db.Categories.AsNoTracking().SingleAsync(c => c.Id == category.Id);
            Assert.Equal(category.Name, saved.Name);
            Assert.Equal(category.Description, saved.Description);
        }
        var expected = new Dictionary<string, int>
        {
            ["Laptops"] = 7, ["Computers and Monitors"] = 5, ["Keyboards"] = 5,
            ["Mice"] = 5, ["Headphones"] = 4, ["Other Accessories"] = 4
        };
        foreach (var (name, count) in expected)
        {
            var category = await db.Categories.SingleAsync(c => c.Name == name);
            var fromApi = await factory.Client.GetFromJsonAsync<List<ProductResponse>>($"/api/products?categoryId={category.Id}");
            Assert.Equal(count, fromApi!.Count);
            Assert.All(fromApi, p => Assert.Equal(category.Id, p.CategoryId));
        }
        var all = await factory.Client.GetFromJsonAsync<List<ProductResponse>>("/api/products");
        Assert.Equal(30, all!.Count);
        Assert.Equal(30, all.Select(p => p.Name.ToUpperInvariant()).Distinct().Count());
        foreach (var product in all)
        {
            var detail = await factory.Client.GetFromJsonAsync<ProductResponse>($"/api/products/{product.Id}");
            Assert.Equal(product, detail);
            Assert.True(product.Price > 0 && product.StockQuantity >= 0);
            var search = await factory.Client.GetFromJsonAsync<List<ProductResponse>>($"/api/products?search={Uri.EscapeDataString(product.Name)}");
            Assert.Contains(search!, match => match.Id == product.Id);
        }

        // A fresh DbContext models application restart, not just a second call on tracked entities.
        await using (var restarted = factory.Services.CreateAsyncScope())
            await restarted.ServiceProvider.GetRequiredService<CatalogSeeder>().SeedAsync();
        Assert.Equal(30, await db.Products.CountAsync());
        Assert.Equal(9, await db.Categories.CountAsync());
        Assert.Equal(ordersBefore, await OrderSnapshot(db));

        // Administrator-added rows above the target must never be removed on restart.
        db.Products.Add(new Product { Name = "User-added product", Price = 10m, StockQuantity = 1, Category = qaCategory });
        await db.SaveChangesAsync();
        await using (var restarted = factory.Services.CreateAsyncScope())
            await restarted.ServiceProvider.GetRequiredService<CatalogSeeder>().SeedAsync();
        Assert.Equal(31, await db.Products.CountAsync());
    }

    private static async Task<string> ProductSnapshot(OnlineStoreDbContext db, List<int> ids) =>
        JsonSerializer.Serialize(await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id)).OrderBy(p => p.Id)
            .Select(p => new { p.Id, p.Name, p.Description, p.Price, p.StockQuantity }).ToListAsync());
    private static async Task<string> CustomerSnapshot(OnlineStoreDbContext db) =>
        JsonSerializer.Serialize(await db.Customers.AsNoTracking().OrderBy(c => c.Id)
            .Select(c => new { c.Id, c.FirstName, c.LastName, c.Email, c.Phone }).ToListAsync());
    private static async Task<string> OrderSnapshot(OnlineStoreDbContext db) =>
        JsonSerializer.Serialize(await db.Orders.AsNoTracking().OrderBy(o => o.Id)
            .Select(o => new { o.Id, o.CustomerId, o.OrderDate, o.Status, o.TotalAmount }).ToListAsync());
    private static async Task<string> ItemSnapshot(OnlineStoreDbContext db) =>
        JsonSerializer.Serialize(await db.OrderItems.AsNoTracking().OrderBy(i => i.Id)
            .Select(i => new { i.Id, i.OrderId, i.ProductId, i.Quantity, i.UnitPrice }).ToListAsync());
}
