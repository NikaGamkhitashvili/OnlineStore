using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OnlineStore.API.DTOs;
using OnlineStore.Infrastructure.Data;
using Xunit;

namespace OnlineStore.Tests;

public class ApiIntegrationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private HttpClient Client => factory.Client;

    private async Task<T> Create<T>(string resource, object body)
    {
        using var response = await Client.PostAsJsonAsync("/api/" + resource, body);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        Assert.NotNull(response.Headers.Location);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<(CategoryResponse Category, ProductResponse Product, CustomerResponse Customer)> Scenario(int stock = 10, decimal price = 12.50m)
    {
        var category = await Create<CategoryResponse>("categories", new { name = "Test " + Guid.NewGuid(), description = "Demo" });
        var product = await Create<ProductResponse>("products", new { name = "Mouse", description = "USB", price, stockQuantity = stock, categoryId = category.Id });
        var customer = await Create<CustomerResponse>("customers", new { firstName = "Nino", lastName = "Beridze", email = $"{Guid.NewGuid():N}@example.com" });
        return (category, product, customer);
    }

    private Task<OrderResponse> Order(int customerId, int productId, int quantity = 1) =>
        Create<OrderResponse>("orders", new { customerId, items = new[] { new { productId, quantity } } });

    private async Task<int> Stock(int id) => (await Client.GetFromJsonAsync<ProductResponse>($"/api/products/{id}"))!.StockQuantity;

    [Fact]
    public async Task All_twenty_required_operations_work_with_real_SQL_Server()
    {
        var (category, product, customer) = await Scenario();
        foreach (var resource in new[] { "categories", "products", "customers", "orders" })
            Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync("/api/" + resource)).StatusCode);
        Assert.Equal(category.Id, (await Client.GetFromJsonAsync<CategoryResponse>($"/api/categories/{category.Id}"))!.Id);
        Assert.Equal(product.Id, (await Client.GetFromJsonAsync<ProductResponse>($"/api/products/{product.Id}"))!.Id);
        Assert.Equal(customer.Id, (await Client.GetFromJsonAsync<CustomerResponse>($"/api/customers/{customer.Id}"))!.Id);

        var categoryUpdate = await Client.PutAsJsonAsync($"/api/categories/{category.Id}", new { name = "Updated category", description = "Updated" });
        Assert.Equal("Updated category", (await categoryUpdate.Content.ReadFromJsonAsync<CategoryResponse>())!.Name);
        var productUpdate = await Client.PutAsJsonAsync($"/api/products/{product.Id}", new { name = "Updated mouse", price = 20.25m, stockQuantity = 10, categoryId = category.Id });
        Assert.Equal(20.25m, (await productUpdate.Content.ReadFromJsonAsync<ProductResponse>())!.Price);
        var customerUpdate = await Client.PutAsJsonAsync($"/api/customers/{customer.Id}", new { firstName = "Giorgi", lastName = "Beridze", email = customer.Email });
        Assert.Equal("Giorgi", (await customerUpdate.Content.ReadFromJsonAsync<CustomerResponse>())!.FirstName);

        var order = await Order(customer.Id, product.Id, 2);
        Assert.Equal(40.50m, order.TotalAmount);
        var fetched = await Client.GetFromJsonAsync<OrderResponse>($"/api/orders/{order.Id}");
        Assert.Equal(2, Assert.Single(fetched!.Items).Quantity);
        var cancelled = await Client.PutAsJsonAsync($"/api/orders/{order.Id}/status", new { status = "Cancelled" });
        Assert.Equal("Cancelled", (await cancelled.Content.ReadFromJsonAsync<OrderResponse>())!.Status);

        foreach (var path in new[] { $"orders/{order.Id}", $"products/{product.Id}", $"customers/{customer.Id}", $"categories/{category.Id}" })
        {
            Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync("/api/" + path)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync("/api/" + path)).StatusCode);
        }
    }

    [Theory]
    [InlineData("products", "{\"name\":\"Mouse\",\"price\":1,\"stockQuantity\":0,\"categoryId\":1}")]
    [InlineData("categories", "{\"name\":\"Category\"}")]
    [InlineData("customers", "{\"firstName\":\"Nino\",\"lastName\":\"B\",\"email\":\"missing@example.com\"}")]
    [InlineData("orders", "{\"status\":\"Completed\"}")]
    public async Task Missing_records_return_404_for_get_update_delete(string resource, string json)
    {
        var path = $"/api/{resource}/{int.MaxValue}";
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync(path)).StatusCode);
        var update = await Client.PutAsync(resource == "orders" ? path + "/status" : path, new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.DeleteAsync(path)).StatusCode);
    }

    [Theory]
    [InlineData("products", "{\"name\":\"  \",\"price\":1,\"stockQuantity\":0,\"categoryId\":1}")]
    [InlineData("products", "{\"name\":\"Mouse\",\"price\":0,\"stockQuantity\":0,\"categoryId\":1}")]
    [InlineData("products", "{\"name\":\"Mouse\",\"price\":-1,\"stockQuantity\":0,\"categoryId\":1}")]
    [InlineData("products", "{\"name\":\"Mouse\",\"price\":1.001,\"stockQuantity\":0,\"categoryId\":1}")]
    [InlineData("products", "{\"name\":\"Mouse\",\"price\":1,\"stockQuantity\":-1,\"categoryId\":1}")]
    [InlineData("products", "{\"name\":\"Mouse\",\"price\":1,\"categoryId\":1}")]
    [InlineData("categories", "{\"name\":\"  \"}")]
    [InlineData("customers", "{\"firstName\":\"Nino\",\"lastName\":\"B\",\"email\":\"invalid\"}")]
    [InlineData("customers", "{\"firstName\":\"\",\"lastName\":\"B\",\"email\":\"n@example.com\"}")]
    [InlineData("orders", "{\"customerId\":1,\"items\":[]}")]
    [InlineData("orders", "{\"customerId\":1,\"items\":null}")]
    [InlineData("orders", "{\"customerId\":1,\"items\":[null]}")]
    [InlineData("orders", "{\"customerId\":1,\"items\":[{\"productId\":1,\"quantity\":0}]}")]
    public async Task Invalid_requests_return_problem_details_400(string resource, string json)
    {
        var response = await Client.PostAsync("/api/" + resource, new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.True(problem.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task Maximum_lengths_and_foreign_keys_are_validated()
    {
        var (category, product, customer) = await Scenario();
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PostAsJsonAsync("/api/categories", new { name = new string('x', 101) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PostAsJsonAsync("/api/products", new { name = "X", description = new string('x', 501), price = 1m, stockQuantity = 0, categoryId = category.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PostAsJsonAsync("/api/products", new { name = "X", price = 1m, stockQuantity = 0, categoryId = int.MaxValue })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PutAsJsonAsync($"/api/products/{product.Id}", new { name = "X", price = 1m, stockQuantity = 0, categoryId = int.MaxValue })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PostAsJsonAsync("/api/customers", new { firstName = new string('x', 51), lastName = "B", email = "length@example.com" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PostAsJsonAsync("/api/orders", new { customerId = int.MaxValue, items = new[] { new { productId = product.Id, quantity = 1 } } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PostAsJsonAsync("/api/orders", new { customerId = customer.Id, items = new[] { new { productId = int.MaxValue, quantity = 1 } } })).StatusCode);
    }

    [Fact]
    public async Task Duplicate_email_is_case_insensitive_and_safe_under_concurrent_posts()
    {
        var (_, _, customer) = await Scenario();
        var duplicate = new { firstName = "Nino", lastName = "B", email = customer.Email.ToUpperInvariant() };
        Assert.Equal(HttpStatusCode.Conflict, (await Client.PostAsJsonAsync("/api/customers", duplicate)).StatusCode);
        var (_, _, other) = await Scenario();
        Assert.Equal(HttpStatusCode.Conflict, (await Client.PutAsJsonAsync($"/api/customers/{other.Id}", duplicate)).StatusCode);
        var sameEmail = new { firstName = "Nino", lastName = "B", email = $"{Guid.NewGuid():N}@example.com" };
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Client.PostAsJsonAsync("/api/customers", sameEmail)));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Equal(4, results.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task Order_total_stock_and_purchase_price_are_server_controlled()
    {
        var (category, product, customer) = await Scenario(price: 12.35m);
        var second = await Create<ProductResponse>("products", new { name = "Keyboard", price = 25.10m, stockQuantity = 6, categoryId = category.Id });
        var order = await Create<OrderResponse>("orders", new
        {
            customerId = customer.Id, totalAmount = 0.01m,
            items = new[] { new { productId = product.Id, quantity = 3, unitPrice = 0.01m }, new { productId = second.Id, quantity = 2, unitPrice = 0.01m } }
        });
        Assert.Equal(87.25m, order.TotalAmount);
        Assert.Equal(7, await Stock(product.Id));
        Assert.Equal(4, await Stock(second.Id));
        Assert.Equal(12.35m, order.Items.Single(i => i.ProductId == product.Id).UnitPrice);
        Assert.Equal("Pending", order.Status);
        Assert.Equal(DateTimeKind.Utc, order.OrderDate.Kind);
        Assert.Equal(HttpStatusCode.OK, (await Client.PutAsJsonAsync($"/api/products/{product.Id}", new { name = "Mouse", price = 99m, stockQuantity = 7, categoryId = category.Id })).StatusCode);
        var old = await Client.GetFromJsonAsync<OrderResponse>($"/api/orders/{order.Id}");
        Assert.Equal(87.25m, old!.TotalAmount);
        Assert.Equal(12.35m, old.Items.Single(i => i.ProductId == product.Id).UnitPrice);
    }

    [Fact]
    public async Task Failed_multi_item_orders_roll_back_all_order_and_stock_changes()
    {
        var (category, product, customer) = await Scenario(stock: 5);
        var empty = await Create<ProductResponse>("products", new { name = "Empty", price = 1m, stockQuantity = 0, categoryId = category.Id });
        var before = (await Client.GetFromJsonAsync<List<OrderResponse>>("/api/orders"))!.Count;
        var response = await Client.PostAsJsonAsync("/api/orders", new { customerId = customer.Id, items = new[] { new { productId = product.Id, quantity = 2 }, new { productId = empty.Id, quantity = 1 } } });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(5, await Stock(product.Id));
        Assert.Equal(0, await Stock(empty.Id));
        Assert.Equal(before, (await Client.GetFromJsonAsync<List<OrderResponse>>("/api/orders"))!.Count);
        var duplicate = await Client.PostAsJsonAsync("/api/orders", new { customerId = customer.Id, items = new[] { new { productId = product.Id, quantity = 3 }, new { productId = product.Id, quantity = 3 } } });
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Equal(5, await Stock(product.Id));
    }

    [Fact]
    public async Task Concurrent_orders_never_oversell_stock()
    {
        var (_, product, customer) = await Scenario(stock: 5);
        var responses = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Client.PostAsJsonAsync("/api/orders", new { customerId = customer.Id, items = new[] { new { productId = product.Id, quantity = 1 } } })));
        Assert.Equal(5, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(7, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(0, await Stock(product.Id));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OnlineStoreDbContext>();
        Assert.Equal(5, await db.OrderItems.Where(i => i.ProductId == product.Id).SumAsync(i => i.Quantity));
    }

    [Fact]
    public async Task Status_transitions_deletion_restrictions_and_stock_restoration_work()
    {
        var (category, product, customer) = await Scenario(stock: 10);
        var order = await Order(customer.Id, product.Id, 2);
        Assert.Equal(HttpStatusCode.Conflict, (await Client.DeleteAsync($"/api/categories/{category.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Client.DeleteAsync($"/api/products/{product.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Client.DeleteAsync($"/api/customers/{customer.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PutAsJsonAsync($"/api/orders/{order.Id}/status", new { status = "Unknown" })).StatusCode);
        for (var i = 0; i < 2; i++)
            Assert.Equal(HttpStatusCode.OK, (await Client.PutAsJsonAsync($"/api/orders/{order.Id}/status", new { status = "Cancelled" })).StatusCode);
        Assert.Equal(10, await Stock(product.Id));
        Assert.Equal(HttpStatusCode.Conflict, (await Client.PutAsJsonAsync($"/api/orders/{order.Id}/status", new { status = "Completed" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync($"/api/orders/{order.Id}")).StatusCode);
        Assert.Equal(10, await Stock(product.Id));
        var pending = await Order(customer.Id, product.Id, 3);
        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync($"/api/orders/{pending.Id}")).StatusCode);
        Assert.Equal(10, await Stock(product.Id));
        var completed = await Order(customer.Id, product.Id);
        Assert.Equal(HttpStatusCode.OK, (await Client.PutAsJsonAsync($"/api/orders/{completed.Id}/status", new { status = "Completed" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Client.DeleteAsync($"/api/orders/{completed.Id}")).StatusCode);
        Assert.Equal(9, await Stock(product.Id));
    }

    [Fact]
    public async Task LINQ_filters_and_SQL_reporting_objects_work()
    {
        var (category, product, customer) = await Scenario();
        var order = await Order(customer.Id, product.Id);
        var list = await Client.GetFromJsonAsync<List<ProductResponse>>($"/api/products?search=Mouse&categoryId={category.Id}");
        Assert.Equal(product.Id, Assert.Single(list!).Id);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OnlineStoreDbContext>();
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM dbo.vw_CategoryProductSummary WHERE CategoryId = @id AND ProductCount = 1;";
        var parameter = command.CreateParameter(); parameter.ParameterName = "@id"; parameter.Value = category.Id; command.Parameters.Add(parameter);
        Assert.Equal(1, Convert.ToInt32(await command.ExecuteScalarAsync()));
        command.Parameters.Clear();
        command.CommandText = "EXEC dbo.GetOrderDetails @OrderId = @id";
        parameter = command.CreateParameter(); parameter.ParameterName = "@id"; parameter.Value = order.Id; command.Parameters.Add(parameter);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(order.Id, reader.GetInt32(0));
    }

    [Fact]
    public async Task Development_seed_is_repeatable_and_never_overwrites_existing_data()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OnlineStoreDbContext>();
        // An isolated second database is unnecessary: this test verifies the nonempty-store guard.
        var (_, product, _) = await Scenario(stock: 17);
        var before = await db.Products.CountAsync();
        var seeder = scope.ServiceProvider.GetRequiredService<DevelopmentSeeder>();
        await seeder.SeedAsync();
        await seeder.SeedAsync();
        Assert.Equal(before, await db.Products.CountAsync());
        Assert.Equal(17, await Stock(product.Id));
    }
}
