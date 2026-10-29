using OnlineStore.Core.Entities;

namespace OnlineStore.Core.Interfaces;

public interface IProductService
{
    Task<IReadOnlyList<Product>> GetAllAsync(string? search = null, int? categoryId = null, CancellationToken ct = default);
    Task<Product?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Product> CreateAsync(Product product, CancellationToken ct = default);
    Task<Product> UpdateAsync(int id, Product product, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
