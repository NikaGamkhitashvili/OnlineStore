namespace OnlineStore.Core.Interfaces;

public record ExchangeRate(string BaseCurrency, string TargetCurrency, DateOnly Date, decimal Rate);

public interface IExchangeRateService
{
    Task<ExchangeRate> GetAsync(string baseCurrency, string targetCurrency, CancellationToken ct = default);
}
