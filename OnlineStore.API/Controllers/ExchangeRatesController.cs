using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using OnlineStore.Core.Interfaces;
using OnlineStore.Core.Exceptions;

namespace OnlineStore.API.Controllers;

[Route("api/exchange-rates")]
public class ExchangeRatesController(IExchangeRateService service) : StoreControllerBase
{
    [HttpGet]
    [ProducesResponseType<ExchangeRate>(200)]
    [ProducesResponseType<ProblemDetails>(502)]
    [ProducesResponseType<ProblemDetails>(503)]
    [ProducesResponseType<ProblemDetails>(504)]
    public async Task<ActionResult<ExchangeRate>> Get(
        [FromQuery, RegularExpression("^[A-Za-z]{3}$")] string baseCurrency = "USD",
        [FromQuery, RegularExpression("^[A-Za-z]{3}$")] string targetCurrency = "GEL",
        CancellationToken ct = default)
    {
        // Model binding can replace an explicitly empty optional query value with its default.
        // Missing codes use defaults; explicitly empty codes are invalid input.
        if ((Request.Query.TryGetValue(nameof(baseCurrency), out var source) && string.IsNullOrWhiteSpace(source.ToString()))
            || (Request.Query.TryGetValue(nameof(targetCurrency), out var target) && string.IsNullOrWhiteSpace(target.ToString())))
            throw new StoreException("Both currency codes are required.");
        return Ok(await service.GetAsync(baseCurrency, targetCurrency, ct));
    }
}
