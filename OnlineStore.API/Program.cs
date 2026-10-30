using Microsoft.EntityFrameworkCore;
using OnlineStore.Infrastructure.Data;
using OnlineStore.Infrastructure.Services;
using OnlineStore.Core.Interfaces;
using OnlineStore.API.Middleware;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.SwaggerDoc("v1", new OpenApiInfo
{
    Title = "OnlineStore.API", Version = "v1", Description = "Online store management: products, categories, customers, orders and exchange rates."
}));
builder.Services.AddProblemDetails();

var connectionString = builder.Configuration.GetConnectionString("OnlineStoreDb")
    ?? throw new InvalidOperationException("Connection string 'OnlineStoreDb' is missing.");
builder.Services.AddDbContext<OnlineStoreDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddScoped<StoreTransaction>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<DevelopmentSeeder>();
builder.Services.AddScoped<CatalogSeeder>();
builder.Services.AddHttpClient<IExchangeRateService, ExchangeRateService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ExchangeRates:BaseUrl"] ?? "https://api.frankfurter.dev/");
    client.Timeout = TimeSpan.FromSeconds(10);
});

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();
app.UseStatusCodePages();
app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
// Known storefront routes only: unknown /api paths must remain API 404 responses.
foreach (var route in new[] { "/products", "/products/{id:int}", "/cart", "/checkout", "/orders", "/orders/{id:int}", "/admin" })
    app.MapFallbackToFile(route, "index.html");

if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("SeedData:Enabled"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<DevelopmentSeeder>().SeedAsync();
    if (app.Configuration.GetValue<bool>("SeedData:CatalogEnabled"))
        await scope.ServiceProvider.GetRequiredService<CatalogSeeder>().SeedAsync();
}

app.Run();

public partial class Program;
