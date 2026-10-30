using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace OnlineStore.Tests;

public class SecurityTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // Isolated fixture value, never a deployment credential.
    private const string TestPassword = "Isolated-test-fixture-32-characters";
    private WebApplicationFactory<Program> Host(bool configured = true) => factory.WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Production");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        { ["Admin:Password"] = configured ? TestPassword : "", ["Hosting:RenderProxy"] = "true" }));
    });

    [Theory]
    [InlineData("GET", "/api/customers")]
    [InlineData("GET", "/api/orders")]
    [InlineData("POST", "/api/products")]
    [InlineData("PUT", "/api/products/1")]
    [InlineData("DELETE", "/api/categories/1")]
    [InlineData("POST", "/api/orders")]
    public async Task Anonymous_users_cannot_read_customer_data_or_change_store(string method, string url)
    {
        using var host = Host(); using var client = host.CreateClient(new() { BaseAddress = new("https://localhost") });
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(new(new HttpMethod(method), url))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/products")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/categories")).StatusCode);
    }

    [Fact]
    public async Task Login_uses_secure_cookie_requires_request_header_and_logout_revokes_session()
    {
        using var host = Host(); using var client = host.CreateClient(new() { BaseAddress = new("https://localhost") });
        var body = new { username = "admin", password = TestPassword };
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/auth/login", body)).StatusCode);
        client.DefaultRequestHeaders.Add("X-Store-Request", "1");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { username = "admin", password = "wrong" })).StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", body);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/customers")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/products", new { })).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Store-Request");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync("/api/products/1")).StatusCode);
        client.DefaultRequestHeaders.Add("X-Store-Request", "1");
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/customers")).StatusCode);
    }

    [Fact]
    public async Task Missing_credentials_fail_closed_and_login_attempts_are_limited()
    {
        using var unconfigured = Host(false); using var unavailable = unconfigured.CreateClient(new() { BaseAddress = new("https://localhost") });
        unavailable.DefaultRequestHeaders.Add("X-Store-Request", "1");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await unavailable.PostAsJsonAsync("/api/auth/login", new { username = "admin", password = TestPassword })).StatusCode);
        using var host = Host(); using var client = host.CreateClient(new() { BaseAddress = new("https://localhost") });
        client.DefaultRequestHeaders.Add("X-Store-Request", "1");
        for (var i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { username = "admin", password = "incorrect" })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/auth/login", new { username = "admin", password = "incorrect" })).StatusCode);
    }

    [Fact]
    public async Task Render_forwarded_https_does_not_loop_and_health_has_no_secrets()
    {
        using var host = Host(); using var client = host.CreateClient(new() { BaseAddress = new("http://localhost"), AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.TemporaryRedirect, (await client.GetAsync("/health")).StatusCode);
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        Assert.DoesNotContain(TestPassword, await client.GetStringAsync("/health"));
    }
}
