using Microsoft.AspNetCore.Mvc;
using OnlineStore.API.DTOs;
using OnlineStore.Core.Exceptions;
using OnlineStore.Core.Interfaces;

namespace OnlineStore.API.Controllers;

[Route("api/products")]
public class ProductsController(IProductService service) : StoreControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ProductResponse>>(200)]
    public async Task<ActionResult<IReadOnlyList<ProductResponse>>> GetAll([FromQuery] string? search, [FromQuery, System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)] int? categoryId, CancellationToken ct) =>
        Ok((await service.GetAllAsync(search, categoryId, ct)).Select(ProductResponse.From).ToList());

    [HttpGet("{id:int}")]
    [ProducesResponseType<ProductResponse>(200)]
    public async Task<ActionResult<ProductResponse>> GetById(int id, CancellationToken ct)
    {
        var entity = await service.GetByIdAsync(id, ct) ?? throw new NotFoundException("Product", id);
        return Ok(ProductResponse.From(entity));
    }

    [HttpPost]
    [ProducesResponseType<ProductResponse>(201)]
    public async Task<ActionResult<ProductResponse>> Create(ProductRequest request, CancellationToken ct)
    {
        var entity = await service.CreateAsync(request.ToEntity(), ct);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, ProductResponse.From(entity));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<ProductResponse>(200)]
    public async Task<ActionResult<ProductResponse>> Update(int id, ProductRequest request, CancellationToken ct) =>
        Ok(ProductResponse.From(await service.UpdateAsync(id, request.ToEntity(), ct)));

    [HttpDelete("{id:int}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}
