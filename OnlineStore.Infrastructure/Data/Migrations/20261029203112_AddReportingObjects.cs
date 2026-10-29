using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineStore.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReportingObjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Additive only: existing tables and rows are untouched.
            migrationBuilder.Sql("""
                CREATE VIEW dbo.vw_CategoryProductSummary AS
                SELECT c.Id AS CategoryId, c.Name AS CategoryName,
                       COUNT(p.Id) AS ProductCount,
                       COALESCE(SUM(CAST(p.StockQuantity AS bigint)), 0) AS TotalStock
                FROM dbo.Categories c
                LEFT JOIN dbo.Products p ON p.CategoryId = c.Id
                GROUP BY c.Id, c.Name;
                """);
            migrationBuilder.Sql("""
                CREATE PROCEDURE dbo.GetOrderDetails @OrderId int AS
                BEGIN
                    SET NOCOUNT ON;
                    SELECT o.Id AS OrderId, o.CustomerId, o.OrderDate, o.Status, o.TotalAmount,
                           i.Id AS OrderItemId, i.ProductId, p.Name AS ProductName,
                           i.Quantity, i.UnitPrice, i.Quantity * i.UnitPrice AS Subtotal
                    FROM dbo.Orders o
                    JOIN dbo.OrderItems i ON i.OrderId = o.Id
                    JOIN dbo.Products p ON p.Id = i.ProductId
                    WHERE o.Id = @OrderId
                    ORDER BY i.Id;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE dbo.GetOrderDetails;");
            migrationBuilder.Sql("DROP VIEW dbo.vw_CategoryProductSummary;");
        }
    }
}
