using Microsoft.EntityFrameworkCore;
using OnlineStore.Core.Entities;

namespace OnlineStore.Infrastructure.Data;

public class DevelopmentSeeder(OnlineStoreDbContext db, StoreTransaction transaction)
{
    public async Task SeedAsync(CancellationToken ct = default) => await transaction.RunAsync(async () =>
    {
        // Seed only a completely empty store. Never repair, replace or overwrite user data.
        if (await db.Categories.AnyAsync(ct) || await db.Products.AnyAsync(ct)
            || await db.Customers.AnyAsync(ct) || await db.Orders.AnyAsync(ct)) return false;

        var computers = new Category { Name = "კომპიუტერები", Description = "ლეპტოპები და კომპიუტერის მოწყობილობები" };
        var accessories = new Category { Name = "აქსესუარები", Description = "ყოველდღიური აქსესუარები" };
        var laptop = new Product { Name = "Lenovo IdeaPad 15", Description = "16GB RAM, 512GB SSD", Price = 1899.99m, StockQuantity = 8, Category = computers };
        var mouse = new Product { Name = "Logitech M185", Description = "უსადენო მაუსი", Price = 39.90m, StockQuantity = 30, Category = accessories };
        var keyboard = new Product { Name = "Logitech K120", Description = "USB კლავიატურა", Price = 49.50m, StockQuantity = 20, Category = accessories };
        var customer = new Customer { FirstName = "ნინო", LastName = "ბერიძე", Email = "nino.demo@example.com", Phone = "+995555010101" };
        var otherCustomer = new Customer { FirstName = "გიორგი", LastName = "მაისურაძე", Email = "giorgi.demo@example.com" };
        var order = new Order
        {
            Customer = customer, OrderDate = DateTime.UtcNow, Status = "Pending", TotalAmount = 129.30m,
            Items = new List<OrderItem>
            {
                new() { Product = mouse, Quantity = 2, UnitPrice = mouse.Price },
                new() { Product = keyboard, Quantity = 1, UnitPrice = keyboard.Price }
            }
        };
        mouse.StockQuantity -= 2;
        keyboard.StockQuantity -= 1;
        db.Products.AddRange(laptop, mouse, keyboard);
        db.Customers.Add(otherCustomer);
        db.Orders.Add(order);
        return true;
    }, ct);
}
