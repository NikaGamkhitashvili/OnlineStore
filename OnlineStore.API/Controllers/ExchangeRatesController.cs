using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using OnlineStore.Core.Interfaces;

namespace OnlineStore.API.Controllers;

[Route("api/exchange-rates")]
public class ExchangeRatesController(IExchangeRateService service) : StoreControllerBase
{
    [HttpGet]
    [ProducesResponseType<ProblemDetails>(502)]
    [ProducesResponseType<ProblemDetails>(503)]
    [ProducesResponseType<ProblemDetails>(504)]
    public async Task<ActionResult<ExchangeRate>> Get(
        [FromQuery, RegularExpression("^[A-Za-z]{3}$")] string baseCurrency = "USD",
        [FromQuery, RegularExpression("^[A-Za-z]{3}$")] string targetCurrency = "GEL",
        CancellationToken ct = default) => Ok(await service.GetAsync(baseCurrency, targetCurrency, ct));
}
