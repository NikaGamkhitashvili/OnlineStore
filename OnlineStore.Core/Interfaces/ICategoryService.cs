using OnlineStore.Core.Entities;

namespace OnlineStore.Core.Interfaces;

public interface ICategoryService
{
    Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken ct = default);
    Task<Category?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Category> CreateAsync(Category category, CancellationToken ct = default);
    Task<Category> UpdateAsync(int id, Category category, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
