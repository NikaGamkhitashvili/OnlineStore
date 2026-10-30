namespace OnlineStore.Core.Entities;

// Source metadata is separate from the store's editable price and inventory.
public class ProductImport
{
    public int ProductId { get; set; }
    public int ExternalId { get; set; }
    public string? Brand { get; set; }
    public string SourceJson { get; set; } = "{}";
    public decimal InitialUsdToGelRate { get; set; }
    public DateOnly RateDate { get; set; }
    public DateTime LastSyncedAtUtc { get; set; }
    public Product Product { get; set; } = null!;
}
