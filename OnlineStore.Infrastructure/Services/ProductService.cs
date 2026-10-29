using Microsoft.EntityFrameworkCore;
using OnlineStore.Core.Entities;
using OnlineStore.Core.Exceptions;
using OnlineStore.Core.Interfaces;
using OnlineStore.Infrastructure.Data;

namespace OnlineStore.Infrastructure.Services;

public class ProductService(OnlineStoreDbContext db, StoreTransaction transaction) : IProductService
{
    public async Task<IReadOnlyList<Product>> GetAllAsync(string? search = null, int? categoryId = null, CancellationToken ct = default)
    {
        var query = db.Products.AsNoTracking().Include(p => p.Category).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(p => p.Name.Contains(search.Trim()));
        if (categoryId.HasValue) query = query.Where(p => p.CategoryId == categoryId.Value);
        return await query.OrderBy(p => p.Id).ToListAsync(ct);
    }

    public Task<Product?> GetByIdAsync(int id, CancellationToken ct = default) =>
        db.Products.AsNoTracking().Include(p => p.Category).FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Product> CreateAsync(Product product, CancellationToken ct = default) => transaction.RunAsync(async () =>
    {
        await ValidateAsync(product, ct);
        var entity = new Product();
        Copy(product, entity);
        db.Products.Add(entity);
        return entity;
    }, ct);

    public Task<Product> UpdateAsync(int id, Product product, CancellationToken ct = default) => transaction.RunAsync(async () =>
    {
        var entity = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Product", id);
        await ValidateAsync(product, ct);
        Copy(product, entity);
        return entity;
    }, ct);

    public async Task DeleteAsync(int id, CancellationToken ct = default) => await transaction.RunAsync(async () =>
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Product", id);
        if (await db.OrderItems.AnyAsync(i => i.ProductId == id, ct))
            throw new ConflictException("A product used in an order cannot be deleted.");
        db.Products.Remove(product);
        return true;
    }, ct);

    private async Task ValidateAsync(Product p, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(p.Name) || p.Name.Length > 100 || p.Description.Length > 500)
            throw new StoreException("Product name or description is invalid.");
        if (p.Price <= 0 || p.Price > 9999999999999999.99m || decimal.Round(p.Price, 2) != p.Price || p.StockQuantity < 0)
            throw new StoreException("Price must be positive with at most two decimal places; stock must be nonnegative.");
        if (!await db.Categories.AnyAsync(c => c.Id == p.CategoryId, ct))
            throw new StoreException("The selected category does not exist.");
    }

    private static void Copy(Product source, Product target)
    {
        target.Name = source.Name.Trim();
        target.Description = source.Description.Trim();
        target.Price = source.Price;
        target.StockQuantity = source.StockQuantity;
        target.CategoryId = source.CategoryId;
    }
}
