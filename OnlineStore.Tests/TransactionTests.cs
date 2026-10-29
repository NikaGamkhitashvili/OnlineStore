using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OnlineStore.API.DTOs;
using OnlineStore.Core.Entities;
using OnlineStore.Infrastructure.Data;
using Xunit;

namespace OnlineStore.Tests;

public class TransactionTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private sealed class FailureAfterSave : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken ct = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<Order>().Any())
                throw new InvalidOperationException("Simulated failure after SQL INSERT and UPDATE but before COMMIT.");
            return ValueTask.FromResult(result);
        }
    }

    [Fact]
    public async Task Failure_after_SQL_save_rolls_back_both_order_and_inventory()
    {
        int customerId, productId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OnlineStoreDbContext>();
            var category = new Category { Name = "Rollback" };
            var product = new Product { Name = "Rollback product", Price = 10m, StockQuantity = 5, Category = category };
            var customer = new Customer { FirstName = "Nino", LastName = "B", Email = $"{Guid.NewGuid():N}@example.com" };
            db.Products.Add(product); db.Customers.Add(customer);
            await db.SaveChangesAsync();
            customerId = customer.Id; productId = product.Id;
        }
        using var failingHost = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddDbContext<OnlineStoreDbContext>(options => options.AddInterceptors(new FailureAfterSave()))));
        using var client = failingHost.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var response = await client.PostAsJsonAsync("/api/orders", new { customerId, items = new[] { new { productId, quantity = 2 } } });
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<OnlineStoreDbContext>();
        Assert.Equal(5, (await verificationDb.Products.SingleAsync(p => p.Id == productId)).StockQuantity);
        Assert.False(await verificationDb.Orders.AnyAsync(o => o.CustomerId == customerId));
        Assert.False(await verificationDb.OrderItems.AnyAsync(i => i.ProductId == productId));
    }

    [Fact]
    public async Task SQL_constraints_reject_invalid_price_stock_email_and_foreign_key()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OnlineStoreDbContext>();
        var category = new Category { Name = "Constraints" };
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        foreach (var invalid in new[]
        {
            new Product { Name = "Invalid", Price = 0, StockQuantity = 1, CategoryId = category.Id },
            new Product { Name = "Invalid", Price = 1, StockQuantity = -1, CategoryId = category.Id },
            new Product { Name = "Invalid", Price = 1, StockQuantity = 1, CategoryId = int.MaxValue }
        })
        {
            db.Products.Add(invalid);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.Entry(invalid).State = EntityState.Detached;
        }
        var email = $"{Guid.NewGuid():N}@example.com";
        db.Customers.Add(new Customer { FirstName = "N", LastName = "B", Email = email });
        await db.SaveChangesAsync();
        db.Customers.Add(new Customer { FirstName = "Other", LastName = "B", Email = email });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
