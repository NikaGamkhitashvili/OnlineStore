using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OnlineStore.Core.Entities;
using OnlineStore.Core.Interfaces;
using OnlineStore.Infrastructure.Data;
using Xunit;

namespace OnlineStore.Tests;

public class HostingTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private sealed class UnavailableRateService : IExchangeRateService
    {
        public Task<ExchangeRate> GetAsync(string baseCurrency, string targetCurrency, CancellationToken ct = default) =>
            throw new OnlineStore.Core.Exceptions.StoreException("Provider unavailable.", 503);
    }

    private sealed class BrokenProductService : IProductService
    {
        public Task<IReadOnlyList<Product>> GetAllAsync(string? search = null, int? categoryId = null, CancellationToken ct = default) =>
            throw new InvalidOperationException("SECRET database connection and internal stack details");
        public Task<Product?> GetByIdAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Product> CreateAsync(Product product, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Product> UpdateAsync(int id, Product product, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
    }

    [Fact]
    public async Task Unexpected_errors_are_safe_and_external_failures_are_problem_details()
    {
        using var customized = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IProductService>();
            services.AddScoped<IProductService, BrokenProductService>();
            services.RemoveAll<IExchangeRateService>();
            services.AddScoped<IExchangeRateService, UnavailableRateService>();
        }));
        using var client = customized.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var error = await client.GetAsync("/api/products");
        Assert.Equal(HttpStatusCode.InternalServerError, error.StatusCode);
        var body = await error.Content.ReadAsStringAsync();
        Assert.DoesNotContain("SECRET", body);
        Assert.DoesNotContain("InvalidOperationException", body);
        Assert.Equal("application/problem+json", error.Content.Headers.ContentType!.MediaType);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/exchange-rates")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/exchange-rates?baseCurrency=invalid")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/exchange-rates?baseCurrency=")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/exchange-rates?targetCurrency=%20")).StatusCode);
    }

    [Fact]
    public async Task Swagger_has_twenty_mandatory_operations_plus_external_rate_and_schemas()
    {
        using var development = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("SeedData:Enabled", "false");
        });
        using var client = development.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/swagger/index.html")).StatusCode);
        var spec = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        var paths = spec.GetProperty("paths");
        foreach (var resource in new[] { "products", "categories", "customers", "orders" })
        {
            Assert.True(paths.GetProperty($"/api/{resource}").TryGetProperty("get", out _));
            Assert.True(paths.GetProperty($"/api/{resource}").TryGetProperty("post", out _));
            Assert.True(paths.GetProperty($"/api/{resource}/{{id}}").TryGetProperty("get", out _));
            Assert.True(paths.GetProperty($"/api/{resource}/{{id}}").TryGetProperty("delete", out _));
            Assert.True(paths.GetProperty(resource == "orders" ? "/api/orders/{id}/status" : $"/api/{resource}/{{id}}").TryGetProperty("put", out _));
        }
        Assert.Equal(26, paths.EnumerateObject().Sum(p => p.Value.EnumerateObject().Count()));
        Assert.True(spec.GetProperty("components").GetProperty("schemas").TryGetProperty("CreateOrderRequest", out _));
        Assert.True(spec.GetProperty("components").GetProperty("schemas").TryGetProperty("OrderResponse", out _));
        Assert.True(spec.GetProperty("components").GetProperty("schemas").TryGetProperty("ExchangeRate", out _));
        foreach (var path in paths.EnumerateObject())
        {
            if (path.Name.StartsWith("/api/auth/") || path.Name == "/health") continue;
            foreach (var operation in path.Value.EnumerateObject())
            {
                var status = operation.Name == "post" && path.Name != "/api/catalog-import/dummyjson" ? "201" : operation.Name == "delete" ? "204" : "200";
                var response = operation.Value.GetProperty("responses").GetProperty(status);
                if (status != "204") Assert.True(response.GetProperty("content").GetProperty("application/json").TryGetProperty("schema", out _));
            }
        }
    }

    [Fact]
    public async Task Empty_development_database_seed_creates_consistent_demo_data_once()
    {
        // HostingTests has its own SQL database, and no other test in this class adds domain rows.
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OnlineStoreDbContext>();
        var seeder = scope.ServiceProvider.GetRequiredService<DevelopmentSeeder>();
        await seeder.SeedAsync();
        await seeder.SeedAsync();
        Assert.Equal(2, await db.Categories.CountAsync());
        Assert.Equal(3, await db.Products.CountAsync());
        Assert.Equal(2, await db.Customers.CountAsync());
        Assert.Equal(1, await db.Orders.CountAsync());
        Assert.Equal(2, await db.OrderItems.CountAsync());
        var order = await db.Orders.Include(o => o.Items).SingleAsync();
        Assert.Equal(order.Items.Sum(i => i.Quantity * i.UnitPrice), order.TotalAmount);
        Assert.Equal(28, (await db.Products.SingleAsync(p => p.Name == "Logitech M185")).StockQuantity);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/products")]
    [InlineData("/products/1")]
    [InlineData("/wishlist")]
    [InlineData("/cart")]
    [InlineData("/checkout")]
    [InlineData("/orders")]
    [InlineData("/orders/1")]
    [InlineData("/admin")]
    public async Task Storefront_routes_serve_HTML_without_changing_API_contracts(string route)
    {
        var response = await factory.Client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains("/js/app.js", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Frontend_assets_are_local_and_unknown_API_routes_remain_404()
    {
        foreach (var asset in new[] { "/css/styles.css", "/js/app.js", "/js/api.js", "/js/admin.js", "/js/product-images.js", "/images/logo.svg", "/images/laptop.svg", "/images/mouse.svg", "/images/keyboard.svg", "/images/product.svg", "/images/monitor.svg", "/images/desktop.svg", "/images/headphones.svg", "/images/ssd.svg", "/images/webcam.svg", "/images/hub.svg", "/images/mousepad.svg", "/fonts/NotoSansGeorgian.ttf" })
            Assert.Equal(HttpStatusCode.OK, (await factory.Client.GetAsync(asset)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await factory.Client.GetAsync("/js/product-photos.js")).StatusCode);
        foreach (var asset in new[] { "/js/catalog-data.js", "/js/wishlist-store.js", "/wishlist" })
            Assert.Equal(HttpStatusCode.OK, (await factory.Client.GetAsync(asset)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await factory.Client.GetAsync("/images/products/credits.html")).StatusCode);
        using var photos = JsonDocument.Parse(await factory.Client.GetStringAsync("/images/products/sources.json"));
        Assert.Equal(10, photos.RootElement.EnumerateObject().Count());
        foreach (var photo in photos.RootElement.EnumerateObject())
        {
            var response = await factory.Client.GetAsync(photo.Value.GetProperty("src").GetString());
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("image/webp", response.Content.Headers.ContentType?.MediaType);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await factory.Client.GetAsync("/api/does-not-exist")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await factory.Client.GetAsync("/missing.js")).StatusCode);
    }
}
