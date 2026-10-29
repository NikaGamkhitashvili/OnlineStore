using Microsoft.EntityFrameworkCore;
using OnlineStore.Core.Entities;
using OnlineStore.Core.Exceptions;
using OnlineStore.Core.Interfaces;
using OnlineStore.Infrastructure.Data;

namespace OnlineStore.Infrastructure.Services;

public class CategoryService(OnlineStoreDbContext db, StoreTransaction transaction) : ICategoryService
{
    public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken ct = default) =>
        await db.Categories.AsNoTracking().OrderBy(c => c.Id).ToListAsync(ct);

    public Task<Category?> GetByIdAsync(int id, CancellationToken ct = default) =>
        db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Category> CreateAsync(Category category, CancellationToken ct = default) => transaction.RunAsync(() =>
    {
        var entity = new Category { Name = category.Name.Trim(), Description = category.Description.Trim() };
        db.Categories.Add(entity);
        return Task.FromResult(entity);
    }, ct);

    public Task<Category> UpdateAsync(int id, Category category, CancellationToken ct = default) => transaction.RunAsync(async () =>
    {
        var entity = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Category", id);
        entity.Name = category.Name.Trim();
        entity.Description = category.Description.Trim();
        return entity;
    }, ct);

    public async Task DeleteAsync(int id, CancellationToken ct = default) => await transaction.RunAsync(async () =>
    {
        var entity = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Category", id);
        if (await db.Products.AnyAsync(p => p.CategoryId == id, ct))
            throw new ConflictException("A category with products cannot be deleted.");
        db.Categories.Remove(entity);
        return true;
    }, ct);
}
