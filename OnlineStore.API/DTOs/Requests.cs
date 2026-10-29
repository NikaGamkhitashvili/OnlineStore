using System.ComponentModel.DataAnnotations;
using OnlineStore.Core.Entities;

namespace OnlineStore.API.DTOs;

public sealed class MoneyAttribute : ValidationAttribute
{
    public MoneyAttribute() => ErrorMessage = "Use a positive amount with at most two decimal places (decimal(18,2)).";
    public override bool IsValid(object? value) => value is decimal amount && amount > 0
        && amount <= 9999999999999999.99m && decimal.Round(amount, 2) == amount;
}

public class ProductRequest
{
    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
    [StringLength(500)] public string Description { get; set; } = string.Empty;
    [Money] public decimal Price { get; set; }
    [Required, Range(0, int.MaxValue)] public int? StockQuantity { get; set; }
    [Range(1, int.MaxValue)] public int CategoryId { get; set; }

    public Product ToEntity() => new()
    {
        Name = Name, Description = Description ?? string.Empty, Price = Price,
        StockQuantity = StockQuantity!.Value, CategoryId = CategoryId
    };
}

public class CategoryRequest
{
    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
    [StringLength(300)] public string Description { get; set; } = string.Empty;
    public Category ToEntity() => new() { Name = Name, Description = Description ?? string.Empty };
}

public class CustomerRequest
{
    [Required, StringLength(50)] public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(50)] public string LastName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(150)] public string Email { get; set; } = string.Empty;
    [StringLength(30)] public string? Phone { get; set; }
    public Customer ToEntity() => new() { FirstName = FirstName, LastName = LastName, Email = Email, Phone = Phone };
}

public class CreateOrderRequest : IValidatableObject
{
    [Range(1, int.MaxValue)] public int CustomerId { get; set; }
    [Required, MinLength(1), MaxLength(100)] public List<OrderItemRequest> Items { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Items is not null && Items.Any(item => item is null))
            yield return new ValidationResult("Order items cannot be null.", [nameof(Items)]);
    }
}

public class OrderItemRequest
{
    [Range(1, int.MaxValue)] public int ProductId { get; set; }
    [Range(1, int.MaxValue)] public int Quantity { get; set; }
}

public class UpdateOrderStatusRequest
{
    [Required, RegularExpression("^(Pending|Completed|Cancelled)$"), StringLength(30)]
    public string Status { get; set; } = string.Empty;
}
