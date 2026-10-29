using Microsoft.EntityFrameworkCore;
using OnlineStore.Core.Entities;

namespace OnlineStore.Infrastructure.Data;

public class OnlineStoreDbContext : DbContext
{
    public OnlineStoreDbContext(DbContextOptions<OnlineStoreDbContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var product = modelBuilder.Entity<Product>();
        product.ToTable("Products", table =>
        {
            table.HasCheckConstraint("CK_Products_Price", "[Price] > 0");
            table.HasCheckConstraint("CK_Products_StockQuantity", "[StockQuantity] >= 0");
        });
        product.HasKey(p => p.Id);
        product.Property(p => p.Name).IsRequired().HasMaxLength(100);
        product.Property(p => p.Description).IsRequired().HasMaxLength(500);
        product.Property(p => p.Price).HasPrecision(18, 2);
        product.HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        var category = modelBuilder.Entity<Category>();
        category.ToTable("Categories");
        category.HasKey(c => c.Id);
        category.Property(c => c.Name).IsRequired().HasMaxLength(100);
        category.Property(c => c.Description).IsRequired().HasMaxLength(300);

        var customer = modelBuilder.Entity<Customer>();
        customer.ToTable("Customers");
        customer.HasKey(c => c.Id);
        customer.Property(c => c.FirstName).IsRequired().HasMaxLength(50);
        customer.Property(c => c.LastName).IsRequired().HasMaxLength(50);
        customer.Property(c => c.Email).IsRequired().HasMaxLength(150);
        customer.Property(c => c.Phone).HasMaxLength(30);
        customer.HasIndex(c => c.Email).IsUnique();

        var order = modelBuilder.Entity<Order>();
        order.ToTable("Orders");
        order.HasKey(o => o.Id);
        order.Property(o => o.OrderDate).HasColumnType("datetime2");
        order.Property(o => o.Status).IsRequired().HasMaxLength(30);
        order.Property(o => o.TotalAmount).HasPrecision(18, 2);
        order.HasOne(o => o.Customer)
            .WithMany(c => c.Orders)
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        var orderItem = modelBuilder.Entity<OrderItem>();
        orderItem.ToTable("OrderItems", table =>
            table.HasCheckConstraint("CK_OrderItems_Quantity", "[Quantity] > 0"));
        orderItem.HasKey(i => i.Id);
        orderItem.Property(i => i.UnitPrice).HasPrecision(18, 2);
        orderItem.HasOne(i => i.Order)
            .WithMany(o => o.Items)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        orderItem.HasOne(i => i.Product)
            .WithMany(p => p.OrderItems)
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
