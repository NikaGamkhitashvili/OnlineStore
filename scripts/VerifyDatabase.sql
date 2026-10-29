-- Read-only verification: this script does not change data or schema.
USE [OnlineStoreDb];
SET NOCOUNT ON;

IF (SELECT COUNT(*) FROM sys.tables
    WHERE schema_id = SCHEMA_ID('dbo')
      AND name IN ('Categories', 'Products', 'Customers', 'Orders', 'OrderItems')) <> 5
    THROW 50000, 'Expected five domain tables.', 1;

IF (SELECT COUNT(*) FROM sys.key_constraints
    WHERE type = 'PK' AND parent_object_id IN
        (OBJECT_ID('dbo.Categories'), OBJECT_ID('dbo.Products'),
         OBJECT_ID('dbo.Customers'), OBJECT_ID('dbo.Orders'), OBJECT_ID('dbo.OrderItems'))) <> 5
    THROW 50001, 'Expected five primary keys.', 1;

IF (SELECT COUNT(*) FROM sys.foreign_keys
    WHERE name IN ('FK_Products_Categories_CategoryId', 'FK_Orders_Customers_CustomerId',
                   'FK_OrderItems_Orders_OrderId', 'FK_OrderItems_Products_ProductId')
      AND is_disabled = 0 AND is_not_trusted = 0) <> 4
    THROW 50002, 'Expected four active, trusted foreign keys.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.Customers')
      AND name = 'IX_Customers_Email' AND is_unique = 1 AND is_disabled = 0)
    THROW 50003, 'Expected unique customer email index.', 1;

IF (SELECT COUNT(*) FROM sys.check_constraints
    WHERE name IN ('CK_Products_Price', 'CK_Products_StockQuantity', 'CK_OrderItems_Quantity')
      AND is_disabled = 0 AND is_not_trusted = 0) <> 3
    THROW 50004, 'Expected three active, trusted check constraints.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory
    WHERE MigrationId = '20261029200952_InitialCreate')
    THROW 50005, 'Initial migration is missing.', 1;

SELECT 'PASS: five tables, five PKs, four FKs, unique email, three checks, initial migration.' AS Result;

IF OBJECT_ID('dbo.vw_CategoryProductSummary', 'V') IS NULL
    OR OBJECT_ID('dbo.GetOrderDetails', 'P') IS NULL
    THROW 50006, 'Reporting view or procedure is missing.', 1;

DECLARE @Expected TABLE (TableName sysname, ColumnName sysname, SqlType sysname,
    StringLength int, DecimalPrecision int, DecimalScale int, IsNullable bit);
INSERT INTO @Expected VALUES
('Categories','Id','int',NULL,NULL,NULL,0),
('Categories','Name','nvarchar',100,NULL,NULL,0),
('Categories','Description','nvarchar',300,NULL,NULL,0),
('Products','Id','int',NULL,NULL,NULL,0),
('Products','Name','nvarchar',100,NULL,NULL,0),
('Products','Description','nvarchar',500,NULL,NULL,0),
('Products','Price','decimal',NULL,18,2,0),
('Products','StockQuantity','int',NULL,NULL,NULL,0),
('Products','CategoryId','int',NULL,NULL,NULL,0),
('Customers','Id','int',NULL,NULL,NULL,0),
('Customers','FirstName','nvarchar',50,NULL,NULL,0),
('Customers','LastName','nvarchar',50,NULL,NULL,0),
('Customers','Email','nvarchar',150,NULL,NULL,0),
('Customers','Phone','nvarchar',30,NULL,NULL,1),
('Orders','Id','int',NULL,NULL,NULL,0),
('Orders','CustomerId','int',NULL,NULL,NULL,0),
('Orders','OrderDate','datetime2',NULL,NULL,NULL,0),
('Orders','Status','nvarchar',30,NULL,NULL,0),
('Orders','TotalAmount','decimal',NULL,18,2,0),
('OrderItems','Id','int',NULL,NULL,NULL,0),
('OrderItems','OrderId','int',NULL,NULL,NULL,0),
('OrderItems','ProductId','int',NULL,NULL,NULL,0),
('OrderItems','Quantity','int',NULL,NULL,NULL,0),
('OrderItems','UnitPrice','decimal',NULL,18,2,0);

IF EXISTS (
    SELECT 1 FROM @Expected e
    LEFT JOIN sys.tables t ON t.name = e.TableName AND t.schema_id = SCHEMA_ID('dbo')
    LEFT JOIN sys.columns c ON c.object_id = t.object_id AND c.name = e.ColumnName
    LEFT JOIN sys.types ty ON ty.user_type_id = c.user_type_id
    WHERE c.column_id IS NULL OR ty.name <> e.SqlType OR c.is_nullable <> e.IsNullable
       OR (e.StringLength IS NOT NULL AND c.max_length <> e.StringLength * 2)
       OR (e.DecimalPrecision IS NOT NULL AND c.precision <> e.DecimalPrecision)
       OR (e.DecimalScale IS NOT NULL AND c.scale <> e.DecimalScale)
       OR (e.ColumnName = 'Id' AND c.is_identity <> 1))
    THROW 50007, 'Column definition differs from the required schema.', 1;

IF (SELECT COUNT(*) FROM sys.columns WHERE object_id IN
    (OBJECT_ID('dbo.Categories'), OBJECT_ID('dbo.Products'), OBJECT_ID('dbo.Customers'),
     OBJECT_ID('dbo.Orders'), OBJECT_ID('dbo.OrderItems'))) <> 24
    THROW 50008, 'Unexpected domain columns.', 1;

SELECT 'PASS: exact 24 columns, lengths, decimal precision, nullability, identity, reporting view/procedure.' AS Result;

SELECT t.name AS TableName, c.name AS ColumnName, ty.name AS SqlType,
       CASE WHEN ty.name = 'nvarchar' THEN c.max_length / 2 ELSE c.max_length END AS MaxLength,
       c.precision, c.scale, c.is_nullable, c.is_identity
FROM sys.tables AS t
JOIN sys.columns AS c ON c.object_id = t.object_id
JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
WHERE t.name IN ('Categories', 'Products', 'Customers', 'Orders', 'OrderItems')
ORDER BY t.name, c.column_id;
