using Microsoft.AspNetCore.Mvc;
using OnlineStore.API.DTOs;
using OnlineStore.Core.Exceptions;
using OnlineStore.Core.Interfaces;

namespace OnlineStore.API.Controllers;

[Route("api/orders")]
public class OrdersController(IOrderService service) : StoreControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<OrderResponse>>(200)]
    public async Task<ActionResult<IReadOnlyList<OrderResponse>>> GetAll(CancellationToken ct) =>
        Ok((await service.GetAllAsync(ct)).Select(OrderResponse.From).ToList());

    [HttpGet("{id:int}")]
    [ProducesResponseType<OrderResponse>(200)]
    public async Task<ActionResult<OrderResponse>> GetById(int id, CancellationToken ct)
    {
        var order = await service.GetByIdAsync(id, ct) ?? throw new NotFoundException("Order", id);
        return Ok(OrderResponse.From(order));
    }

    [HttpPost]
    [ProducesResponseType<OrderResponse>(201)]
    public async Task<ActionResult<OrderResponse>> Create(CreateOrderRequest request, CancellationToken ct)
    {
        var items = request.Items.Select(i => new OrderLine(i.ProductId, i.Quantity)).ToList();
        var order = await service.CreateAsync(request.CustomerId, items, ct);
        return CreatedAtAction(nameof(GetById), new { id = order.Id }, OrderResponse.From(order));
    }

    [HttpPut("{id:int}/status")]
    [ProducesResponseType<OrderResponse>(200)]
    public async Task<ActionResult<OrderResponse>> UpdateStatus(int id, UpdateOrderStatusRequest request, CancellationToken ct) =>
        Ok(OrderResponse.From(await service.UpdateStatusAsync(id, request.Status, ct)));

    [HttpDelete("{id:int}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}
