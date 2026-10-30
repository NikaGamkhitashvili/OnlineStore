using OnlineStore.Core.Entities;
using System.Text.Json;

namespace OnlineStore.API.DTOs;

public record ProductResponse(int Id, string Name, string Description, decimal Price, int StockQuantity, int CategoryId)
{
    public ProductImportResponse? ImportMetadata { get; init; }
    public static ProductResponse From(Product p) => new(p.Id, p.Name, p.Description, p.Price, p.StockQuantity, p.CategoryId)
    {
        ImportMetadata = p.ImportMetadata is { } m ? new(m.ExternalId, m.Brand, m.InitialUsdToGelRate,
            m.RateDate, DateTime.SpecifyKind(m.LastSyncedAtUtc, DateTimeKind.Utc), JsonSerializer.Deserialize<JsonElement>(m.SourceJson)) : null
    };
}

public record ProductImportResponse(int ExternalId, string? Brand, decimal InitialUsdToGelRate,
    DateOnly RateDate, DateTime LastSyncedAtUtc, JsonElement Source);

public record CategoryResponse(int Id, string Name, string Description)
{
    public static CategoryResponse From(Category c) => new(c.Id, c.Name, c.Description);
}

public record CustomerResponse(int Id, string FirstName, string LastName, string Email, string? Phone)
{
    public static CustomerResponse From(Customer c) => new(c.Id, c.FirstName, c.LastName, c.Email, c.Phone);
}

public record OrderItemResponse(int Id, int ProductId, int Quantity, decimal UnitPrice, decimal Subtotal);

public record OrderResponse(int Id, int CustomerId, DateTime OrderDate, string Status, decimal TotalAmount,
    IReadOnlyList<OrderItemResponse> Items)
{
    public static OrderResponse From(Order o) => new(o.Id, o.CustomerId,
        DateTime.SpecifyKind(o.OrderDate, DateTimeKind.Utc), o.Status, o.TotalAmount,
        o.Items.OrderBy(i => i.Id).Select(i => new OrderItemResponse(i.Id, i.ProductId, i.Quantity,
            i.UnitPrice, i.UnitPrice * i.Quantity)).ToList());
}
