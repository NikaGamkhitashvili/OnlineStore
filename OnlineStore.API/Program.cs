using Microsoft.EntityFrameworkCore;
using OnlineStore.Infrastructure.Data;
using OnlineStore.Infrastructure.Services;
using OnlineStore.Core.Interfaces;
using OnlineStore.API.Middleware;
using Microsoft.OpenApi;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using OnlineStore.API.Security;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options => options.Filters.Add<StoreAccessFilter>());
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.Name = "__Host-OnlineStore.Admin";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.ExpireTimeSpan = TimeSpan.FromHours(2);
    options.SlidingExpiration = false;
});
builder.Services.AddAuthorization();
builder.Services.AddOptions<Microsoft.AspNetCore.HttpsPolicy.HttpsRedirectionOptions>().Configure<IConfiguration>((options, configuration) =>
{
    if (configuration.GetValue<bool>("Hosting:RenderProxy")) options.HttpsPort = 443;
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
    // Render terminates TLS and forwards to this container over its private ingress.
    // Trust only the scheme, never forwarded client IP/host; enable only on Render.
    if (builder.Configuration.GetValue<bool>("Hosting:RenderProxy"))
    {
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
        options.ForwardLimit = 1;
    }
});
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
builder.Services.AddScoped<ICatalogImportService, CatalogImportService>();
builder.Services.AddHttpClient<IDummyJsonClient, DummyJsonClient>(client =>
{
    client.BaseAddress = new Uri("https://dummyjson.com/");
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddHttpClient<IExchangeRateService, ExchangeRateService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ExchangeRates:BaseUrl"] ?? "https://api.frankfurter.dev/");
    client.Timeout = TimeSpan.FromSeconds(10);
});

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();
app.UseStatusCodePages();
app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseHttpsRedirection();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/auth")) context.Response.Headers.CacheControl = "no-store";
    await next();
});
app.UseDefaultFiles();
var staticOptions = new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        if (Path.GetExtension(context.File.Name) is ".html" or ".js" or ".css")
            context.Context.Response.Headers.CacheControl = "no-cache";
    }
};
app.UseStaticFiles(staticOptions);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
// Known storefront routes only: unknown /api paths must remain API 404 responses.
foreach (var route in new[] { "/products", "/products/{id:int}", "/wishlist", "/cart", "/checkout", "/orders", "/orders/{id:int}", "/admin" })
    app.MapFallbackToFile(route, "index.html", staticOptions);

if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("SeedData:Enabled"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<DevelopmentSeeder>().SeedAsync();
    if (app.Configuration.GetValue<bool>("SeedData:CatalogEnabled"))
        await scope.ServiceProvider.GetRequiredService<CatalogSeeder>().SeedAsync();
}

app.Run();

public partial class Program;
