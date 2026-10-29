using Microsoft.EntityFrameworkCore;
using OnlineStore.Core.Entities;
using OnlineStore.Core.Exceptions;
using OnlineStore.Core.Interfaces;
using OnlineStore.Infrastructure.Data;

namespace OnlineStore.Infrastructure.Services;

public class OrderService(OnlineStoreDbContext db, StoreTransaction transaction) : IOrderService
{
    public async Task<IReadOnlyList<Order>> GetAllAsync(CancellationToken ct = default) =>
        await db.Orders.AsNoTracking().Include(o => o.Items).OrderBy(o => o.Id).ToListAsync(ct);

    public Task<Order?> GetByIdAsync(int id, CancellationToken ct = default) =>
        db.Orders.AsNoTracking().Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id, ct);

    public Task<Order> CreateAsync(int customerId, IReadOnlyList<OrderLine> items, CancellationToken ct = default) => transaction.RunAsync(async () =>
    {
        if (items.Count == 0 || items.Count > 100 || items.Any(i => i.Quantity <= 0 || i.ProductId <= 0))
            throw new StoreException("Provide 1 to 100 items with positive product IDs and quantities.");
        if (items.Select(i => i.ProductId).Distinct().Count() != items.Count)
            throw new StoreException("Each product may appear only once in an order.");
        if (!await db.Customers.AnyAsync(c => c.Id == customerId, ct))
            throw new StoreException("The selected customer does not exist.");

        var ids = items.Select(i => i.ProductId).ToList();
        var products = await db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var order = new Order { CustomerId = customerId, OrderDate = DateTime.UtcNow, Status = "Pending" };
        foreach (var line in items)
        {
            if (!products.TryGetValue(line.ProductId, out var product))
                throw new StoreException($"Product {line.ProductId} does not exist.");
            if (product.StockQuantity < line.Quantity)
                throw new ConflictException($"Insufficient stock for product {product.Id}.");
            order.Items.Add(new OrderItem { ProductId = product.Id, Quantity = line.Quantity, UnitPrice = product.Price });
            order.TotalAmount += product.Price * line.Quantity;
            if (order.TotalAmount > 9999999999999999.99m)
                throw new StoreException("Order total exceeds the supported monetary range.");
        }
        // Only mutate inventory after every line has passed validation.
        foreach (var line in items) products[line.ProductId].StockQuantity -= line.Quantity;
        db.Orders.Add(order);
        return order;
    }, ct);

    public Task<Order> UpdateStatusAsync(int id, string status, CancellationToken ct = default) => transaction.RunAsync(async () =>
    {
        if (status is not ("Pending" or "Completed" or "Cancelled"))
            throw new StoreException("Status must be Pending, Completed or Cancelled.");
        var order = await FindTrackedAsync(id, ct);
        if (order.Status == status) return order;
        if (order.Status != "Pending") throw new ConflictException("A completed or cancelled order is final.");
        if (status == "Cancelled") await RestoreStockAsync(order, ct);
        order.Status = status;
        return order;
    }, ct);

    public async Task DeleteAsync(int id, CancellationToken ct = default) => await transaction.RunAsync(async () =>
    {
        var order = await FindTrackedAsync(id, ct);
        if (order.Status is not ("Pending" or "Cancelled"))
            throw new ConflictException("Only pending or cancelled orders can be deleted.");
        if (order.Status == "Pending") await RestoreStockAsync(order, ct);
        db.Orders.Remove(order);
        return true;
    }, ct);

    private async Task<Order> FindTrackedAsync(int id, CancellationToken ct) =>
        await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id, ct)
        ?? throw new NotFoundException("Order", id);

    private async Task RestoreStockAsync(Order order, CancellationToken ct)
    {
        var ids = order.Items.Select(i => i.ProductId).ToList();
        var products = await db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        foreach (var item in order.Items)
        {
            var product = products[item.ProductId];
            if ((long)product.StockQuantity + item.Quantity > int.MaxValue)
                throw new ConflictException("Restoring this order would exceed the supported stock range.");
            product.StockQuantity += item.Quantity;
        }
    }
}
