using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using OnlineStore.Core.Exceptions;
using OnlineStore.Core.Interfaces;
using OnlineStore.Infrastructure.Services;
using Xunit;

namespace OnlineStore.Tests;

public class ExternalApiTests
{
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> action) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => action(request, ct);
    }

    private static ExchangeRateService Service(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.frankfurter.dev/") });

    [Fact]
    public async Task Valid_provider_response_uses_expected_request_and_maps_rate()
    {
        var service = Service(new Handler((request, _) =>
        {
            Assert.Equal("https://api.frankfurter.dev/v2/rates?base=USD&quotes=GEL", request.RequestUri!.AbsoluteUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""[{"date":"2026-01-01","base":"USD","quote":"GEL","rate":2.70}]""", Encoding.UTF8, "application/json")
            });
        }));
        var result = await service.GetAsync("usd", "gel");
        Assert.Equal(2.70m, result.Rate);
        Assert.Equal("GEL", result.TargetCurrency);
    }

    [Theory]
    [InlineData("not JSON")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("[{}]")]
    [InlineData("[{\"date\":\"2026-01-01\",\"base\":\"USD\",\"quote\":\"EUR\",\"rate\":2}]")]
    [InlineData("[{\"date\":\"2026-01-01\",\"base\":\"USD\",\"quote\":\"GEL\",\"rate\":0}]")]
    [InlineData("[{\"date\":\"bad\",\"base\":\"USD\",\"quote\":\"GEL\",\"rate\":2}]")]
    [InlineData("[{\"date\":\"2099-01-01\",\"base\":\"USD\",\"quote\":\"GEL\",\"rate\":2}]")]
    public async Task Invalid_external_responses_are_502(string body)
    {
        var service = Service(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) })));
        var exception = await Assert.ThrowsAsync<StoreException>(() => service.GetAsync("USD", "GEL"));
        Assert.Equal(502, exception.StatusCode);
    }

    [Fact]
    public async Task Provider_errors_connection_failures_and_timeouts_are_distinct()
    {
        var rejected = Service(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests))));
        Assert.Equal(502, (await Assert.ThrowsAsync<StoreException>(() => rejected.GetAsync("USD", "GEL"))).StatusCode);
        var offline = Service(new Handler((_, _) => throw new HttpRequestException("private network details")));
        Assert.Equal(503, (await Assert.ThrowsAsync<StoreException>(() => offline.GetAsync("USD", "GEL"))).StatusCode);
        var timeout = Service(new Handler((_, _) => throw new TaskCanceledException()));
        Assert.Equal(504, (await Assert.ThrowsAsync<StoreException>(() => timeout.GetAsync("USD", "GEL"))).StatusCode);
    }

    [Theory]
    [InlineData("US", "GEL")]
    [InlineData("", "GEL")]
    [InlineData("USD", " ")]
    [InlineData("USD", "USD")]
    [InlineData("USD", "../")]
    public async Task Invalid_currency_codes_do_not_call_provider(string source, string target)
    {
        var service = Service(new Handler((_, _) => throw new InvalidOperationException("Should not send HTTP")));
        Assert.Equal(400, (await Assert.ThrowsAsync<StoreException>(() => service.GetAsync(source, target))).StatusCode);
    }

    [Fact]
    public async Task Client_cancellation_is_not_reported_as_provider_timeout()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var service = Service(new Handler((_, ct) => Task.FromCanceled<HttpResponseMessage>(ct)));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetAsync("USD", "GEL", cts.Token));
    }
}
