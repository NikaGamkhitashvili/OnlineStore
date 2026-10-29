using Microsoft.AspNetCore.Mvc;
using OnlineStore.API.DTOs;
using OnlineStore.Core.Exceptions;
using OnlineStore.Core.Interfaces;

namespace OnlineStore.API.Controllers;

[Route("api/categories")]
public class CategoriesController(ICategoryService service) : StoreControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryResponse>>> GetAll(CancellationToken ct) =>
        Ok((await service.GetAllAsync(ct)).Select(CategoryResponse.From).ToList());

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CategoryResponse>> GetById(int id, CancellationToken ct)
    {
        var entity = await service.GetByIdAsync(id, ct) ?? throw new NotFoundException("Category", id);
        return Ok(CategoryResponse.From(entity));
    }

    [HttpPost]
    [ProducesResponseType<CategoryResponse>(201)]
    public async Task<ActionResult<CategoryResponse>> Create(CategoryRequest request, CancellationToken ct)
    {
        var entity = await service.CreateAsync(request.ToEntity(), ct);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, CategoryResponse.From(entity));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CategoryResponse>> Update(int id, CategoryRequest request, CancellationToken ct) =>
        Ok(CategoryResponse.From(await service.UpdateAsync(id, request.ToEntity(), ct)));

    [HttpDelete("{id:int}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}
