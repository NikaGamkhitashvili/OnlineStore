using Microsoft.EntityFrameworkCore;
using OnlineStore.Core.Entities;
using OnlineStore.Core.Exceptions;
using OnlineStore.Core.Interfaces;
using OnlineStore.Infrastructure.Data;

namespace OnlineStore.Infrastructure.Services;

public class CustomerService(OnlineStoreDbContext db, StoreTransaction transaction) : ICustomerService
{
    public async Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken ct = default) =>
        await db.Customers.AsNoTracking().OrderBy(c => c.Id).ToListAsync(ct);

    public Task<Customer?> GetByIdAsync(int id, CancellationToken ct = default) =>
        db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Customer> CreateAsync(Customer customer, CancellationToken ct = default) => transaction.RunAsync(async () =>
    {
        await CheckEmailAsync(customer.Email, null, ct);
        var entity = new Customer();
        Copy(customer, entity);
        db.Customers.Add(entity);
        return entity;
    }, ct);

    public Task<Customer> UpdateAsync(int id, Customer customer, CancellationToken ct = default) => transaction.RunAsync(async () =>
    {
        var entity = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Customer", id);
        await CheckEmailAsync(customer.Email, id, ct);
        Copy(customer, entity);
        return entity;
    }, ct);

    public async Task DeleteAsync(int id, CancellationToken ct = default) => await transaction.RunAsync(async () =>
    {
        var entity = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Customer", id);
        if (await db.Orders.AnyAsync(o => o.CustomerId == id, ct))
            throw new ConflictException("A customer with orders cannot be deleted.");
        db.Customers.Remove(entity);
        return true;
    }, ct);

    private async Task CheckEmailAsync(string email, int? id, CancellationToken ct)
    {
        var normalized = email.Trim().ToLowerInvariant();
        if (await db.Customers.AnyAsync(c => c.Id != id && c.Email.ToLower() == normalized, ct))
            throw new ConflictException("This email address is already registered.");
    }

    private static void Copy(Customer source, Customer target)
    {
        target.FirstName = source.FirstName.Trim();
        target.LastName = source.LastName.Trim();
        target.Email = source.Email.Trim().ToLowerInvariant();
        target.Phone = string.IsNullOrWhiteSpace(source.Phone) ? null : source.Phone.Trim();
    }
}
