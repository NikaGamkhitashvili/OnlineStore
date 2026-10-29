using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using OnlineStore.Infrastructure.Data;
using Xunit;

namespace OnlineStore.Tests;

public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string databaseName = "OnlineStoreTests_" + Guid.NewGuid().ToString("N");
    public string ConnectionString => $"Server=localhost;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True;";
    public HttpClient Client { get; private set; } = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<OnlineStoreDbContext>();
            services.RemoveAll<DbContextOptions<OnlineStoreDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<OnlineStoreDbContext>>();
            services.AddDbContext<OnlineStoreDbContext>(options => options.UseSqlServer(ConnectionString));
        });
    }

    public async Task InitializeAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OnlineStoreDbContext>();
        Assert.Equal(databaseName, db.Database.GetDbConnection().Database);
        await db.Database.MigrateAsync();
        Client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await using (var scope = Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OnlineStoreDbContext>();
            // Never allow cleanup to target the development database or a supplied database name.
            var actual = db.Database.GetDbConnection().Database;
            if (actual != databaseName || !System.Text.RegularExpressions.Regex.IsMatch(actual, "^OnlineStoreTests_[a-f0-9]{32}$"))
                throw new InvalidOperationException("Refusing to delete a non-test database.");
            await db.Database.EnsureDeletedAsync();
        }
        Client.Dispose();
        await base.DisposeAsync();
    }
}
