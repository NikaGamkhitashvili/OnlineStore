using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using OnlineStore.Core.Exceptions;
using OnlineStore.Core.Interfaces;

namespace OnlineStore.Infrastructure.Services;

public class ExchangeRateService(HttpClient httpClient) : IExchangeRateService
{
    public async Task<ExchangeRate> GetAsync(string baseCurrency, string targetCurrency, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(baseCurrency) || string.IsNullOrWhiteSpace(targetCurrency))
            throw new StoreException("Both currency codes are required.");
        var source = baseCurrency.Trim().ToUpperInvariant();
        var target = targetCurrency.Trim().ToUpperInvariant();
        if (!Regex.IsMatch(source, "^[A-Z]{3}$") || !Regex.IsMatch(target, "^[A-Z]{3}$") || source == target)
            throw new StoreException("Provide two different three-letter currency codes.");
        try
        {
            using var response = await httpClient.GetAsync($"v2/rates?base={source}&quotes={target}", ct);
            if (!response.IsSuccessStatusCode)
                throw new StoreException("The exchange-rate provider rejected the request.", 502);
            var rates = await response.Content.ReadFromJsonAsync<List<ProviderRate>>(ct);
            if (rates is not { Count: 1 } || rates[0].Base != source || rates[0].Quote != target
                || rates[0].Rate <= 0 || rates[0].Date == default || rates[0].Date > DateOnly.FromDateTime(DateTime.UtcNow))
                throw new StoreException("The exchange-rate provider returned an invalid response.", 502);
            return new ExchangeRate(source, target, rates[0].Date, rates[0].Rate);
        }
        catch (HttpRequestException)
        {
            throw new StoreException("The exchange-rate provider is unavailable.", 503);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new StoreException("The exchange-rate provider timed out.", 504);
        }
        catch (JsonException)
        {
            throw new StoreException("The exchange-rate provider returned invalid JSON.", 502);
        }
    }

    private sealed record ProviderRate(DateOnly Date, string Base, string Quote, decimal Rate);
}
