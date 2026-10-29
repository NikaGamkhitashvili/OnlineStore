using OnlineStore.Core.Entities;

namespace OnlineStore.Core.Interfaces;

public record OrderLine(int ProductId, int Quantity);

public interface IOrderService
{
    Task<IReadOnlyList<Order>> GetAllAsync(CancellationToken ct = default);
    Task<Order?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Order> CreateAsync(int customerId, IReadOnlyList<OrderLine> items, CancellationToken ct = default);
    Task<Order> UpdateStatusAsync(int id, string status, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
