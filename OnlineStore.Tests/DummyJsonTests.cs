using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OnlineStore.API.DTOs;
using OnlineStore.Core.Entities;
using OnlineStore.Core.Exceptions;
using OnlineStore.Core.Interfaces;
using OnlineStore.Infrastructure.Data;
using OnlineStore.Infrastructure.Services;
using Xunit;

namespace OnlineStore.Tests;

public class DummyJsonClientTests
{
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> action) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(action(request));
    }
    public static HttpResponseMessage Response(string category, int id = 1) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(new { total = 1, products = new[] { new { id, title = "Source phone", description = "Source description", category, brand = "Source brand", price = 12.34m, stock = 99, images = new[] { "https://cdn.dummyjson.com/product-images/test.webp" }, rating = 4.7m, reviews = new[] { new { comment = "Original review", rating = 5 } }, customMetadata = "preserved" } } })
    };
    private static DummyJsonClient Client(Func<HttpRequestMessage, HttpResponseMessage> handler) =>
        new(new HttpClient(new Handler(handler)) { BaseAddress = new("https://dummyjson.com/") });

    [Fact]
    public async Task Requests_only_four_technology_categories_and_preserves_all_metadata()
    {
        var seen = new System.Collections.Concurrent.ConcurrentBag<string>();
        var client = Client(request =>
        {
            Assert.Equal("?limit=0", request.RequestUri!.Query);
            var category = request.RequestUri.Segments.Last(); seen.Add(category);
            return Response(category, Array.IndexOf(DummyJsonClient.Categories.Keys.ToArray(), category) + 1);
        });
        var products = await client.GetTechnologyProductsAsync();
        Assert.Equal(4, products.Count);
        Assert.Equal(DummyJsonClient.Categories.Keys.Order(), seen.Order());
        Assert.All(products, p => Assert.Contains("preserved", p.Json));
        Assert.All(products, p => Assert.Equal(12.34m, p.Price));
    }

    [Theory]
    [InlineData("not JSON")]
    [InlineData("null")]
    [InlineData("{\"total\":2,\"products\":[]}")]
    [InlineData("{\"total\":1,\"products\":[{}]}")]
    public async Task Malformed_or_incomplete_payload_is_502(string body)
    {
        var ex = await Assert.ThrowsAsync<StoreException>(() => Client(_ => new(HttpStatusCode.OK) { Content = new StringContent(body) }).GetTechnologyProductsAsync());
        Assert.Equal(502, ex.StatusCode);
    }

    [Fact]
    public async Task Unrelated_categories_and_duplicate_external_ids_are_rejected()
    {
        Assert.Equal(502, (await Assert.ThrowsAsync<StoreException>(() => Client(_ => Response("beauty")).GetTechnologyProductsAsync())).StatusCode);
        Assert.Equal(502, (await Assert.ThrowsAsync<StoreException>(() => Client(r => Response(r.RequestUri!.Segments.Last())).GetTechnologyProductsAsync())).StatusCode);
    }

    [Fact]
    public async Task Network_status_and_timeout_failures_are_safe()
    {
        Assert.Equal(502, (await Assert.ThrowsAsync<StoreException>(() => Client(_ => new(HttpStatusCode.TooManyRequests)).GetTechnologyProductsAsync())).StatusCode);
        Assert.Equal(503, (await Assert.ThrowsAsync<StoreException>(() => Client(_ => throw new HttpRequestException("private details")).GetTechnologyProductsAsync())).StatusCode);
        Assert.Equal(504, (await Assert.ThrowsAsync<StoreException>(() => Client(_ => throw new TaskCanceledException()).GetTechnologyProductsAsync())).StatusCode);
    }
}

public class DummyJsonImportTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private sealed class Rate : IExchangeRateService
    {
        public Task<ExchangeRate> GetAsync(string b, string t, CancellationToken ct = default) => Task.FromResult(new ExchangeRate("USD", "GEL", new DateOnly(2026, 1, 1), 2.5m));
    }
    private sealed class Source : IDummyJsonClient
    {
        public bool Fail { get; set; }
        public async Task<IReadOnlyList<SourceProduct>> GetTechnologyProductsAsync(CancellationToken ct = default)
        {
            if (Fail) throw new StoreException("Provider unavailable", 503);
            using var response = DummyJsonClientTests.Response("smartphones", 999);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            return [new(999, "Source phone", "Source description", "smartphones", "Source brand", 12.34m, json.GetProperty("products")[0].GetRawText())];
        }
    }

    [Fact]
    public async Task Import_is_idempotent_preserves_existing_data_and_admin_edits_and_uses_real_store_inventory()
    {
        var source = new Source();
        using var host = factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IDummyJsonClient>(); s.AddSingleton<IDummyJsonClient>(source);
            s.RemoveAll<IExchangeRateService>(); s.AddSingleton<IExchangeRateService, Rate>();
        }));
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new("https://localhost") });
        var category = (await (await client.PostAsJsonAsync("/api/categories", new { name = "Smartphones" })).Content.ReadFromJsonAsync<CategoryResponse>())!;
        var customer = (await (await client.PostAsJsonAsync("/api/customers", new { firstName = "Import", lastName = "Test", email = "import@example.com" })).Content.ReadFromJsonAsync<CustomerResponse>())!;
        var old = (await (await client.PostAsJsonAsync("/api/products", new { name = "Existing product", price = 20, stockQuantity = 10, categoryId = category.Id })).Content.ReadFromJsonAsync<ProductResponse>())!;
        var oldOrder = await client.PostAsJsonAsync("/api/orders", new { customerId = customer.Id, items = new[] { new { productId = old.Id, quantity = 1 } } });
        var oldOrderJson = await oldOrder.Content.ReadAsStringAsync();
        var oldProductJson = await client.GetStringAsync($"/api/products/{old.Id}");
        var imported = await client.PostAsync("/api/catalog-import/dummyjson", null);
        Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
        Assert.Equal(1, (await imported.Content.ReadFromJsonAsync<CatalogImportResult>())!.Created);
        var products = await client.GetFromJsonAsync<ProductResponse[]>("/api/products?search=Source%20brand");
        var product = Assert.Single(products!);
        Assert.Equal(30.85m, product.Price); Assert.Equal(0, product.StockQuantity);
        Assert.Equal(category.Id, product.CategoryId);
        Assert.Equal(999, product.ImportMetadata!.ExternalId);
        Assert.Equal(99, product.ImportMetadata.Source.GetProperty("stock").GetInt32());
        Assert.Equal("preserved", product.ImportMetadata.Source.GetProperty("customMetadata").GetString());
        var orderRequest = new { customerId = customer.Id, items = new[] { new { productId = product.Id, quantity = 2 } }, totalAmount = 0.01, unitPrice = 0.01 };
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/orders", orderRequest)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/products/{product.Id}", new { name = "Admin title", description = "Admin description", price = 42.55m, stockQuantity = 8, categoryId = category.Id })).StatusCode);
        var again = await (await client.PostAsync("/api/catalog-import/dummyjson", null)).Content.ReadFromJsonAsync<CatalogImportResult>();
        Assert.Equal(new CatalogImportResult(0, 1, 1), again);
        product = (await client.GetFromJsonAsync<ProductResponse>($"/api/products/{product.Id}"))!;
        Assert.Equal("Admin title", product.Name); Assert.Equal("Admin description", product.Description);
        Assert.Equal(42.55m, product.Price); Assert.Equal(8, product.StockQuantity);
        Assert.Equal(2, (await client.GetFromJsonAsync<ProductResponse[]>("/api/products"))!.Length);
        Assert.Single((await client.GetFromJsonAsync<CategoryResponse[]>("/api/categories"))!);
        Assert.Equal(oldProductJson, await client.GetStringAsync($"/api/products/{old.Id}"));
        Assert.Equal(oldOrderJson, await client.GetStringAsync(oldOrder.Headers.Location));
        var checkout = await client.PostAsJsonAsync("/api/orders", orderRequest);
        Assert.Equal(HttpStatusCode.Created, checkout.StatusCode);
        var order = (await checkout.Content.ReadFromJsonAsync<OrderResponse>())!;
        Assert.Equal(85.10m, order.TotalAmount); Assert.Equal(42.55m, Assert.Single(order.Items).UnitPrice);
        Assert.Equal(6, (await client.GetFromJsonAsync<ProductResponse>($"/api/products/{product.Id}"))!.StockQuantity);
        source.Fail = true;
        var before = await client.GetStringAsync("/api/products");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsync("/api/catalog-import/dummyjson", null)).StatusCode);
        Assert.Equal(before, await client.GetStringAsync("/api/products"));
        using var production = factory.WithWebHostBuilder(b => b.UseEnvironment("Production"));
        using var productionClient = production.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new("https://localhost") });
        Assert.Equal(HttpStatusCode.NotFound, (await productionClient.PostAsync("/api/catalog-import/dummyjson", null)).StatusCode);
    }
}
