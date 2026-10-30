using Microsoft.AspNetCore.Mvc;
using OnlineStore.Core.Interfaces;

namespace OnlineStore.API.Controllers;

[Route("api/catalog-import/dummyjson")]
public class CatalogImportController(ICatalogImportService service, IWebHostEnvironment environment) : StoreControllerBase
{
    // The current admin is a local demo without accounts. Do not expose imports in Production.
    [HttpPost]
    [ProducesResponseType<CatalogImportResult>(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Synchronize(CancellationToken ct)
    {
        if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing")) return NotFound();
        return Ok(await service.SynchronizeAsync(ct));
    }
}
