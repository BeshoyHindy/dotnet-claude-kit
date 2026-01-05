// Test Data Builder Pattern
// Copy to: tests/Common/Builders/
// Requires: Your domain types (Order, Customer, etc.)
// Note: Adapt the example builders to match your actual domain entities

namespace YourNamespace.Tests.Common.Builders;

/// <summary>
/// Base class for test data builders.
/// Builders create test objects with sensible defaults that can be customized.
/// </summary>
/// <typeparam name="T">The type being built.</typeparam>
/// <typeparam name="TBuilder">The builder type (for fluent API).</typeparam>
public abstract class Builder<T, TBuilder> where TBuilder : Builder<T, TBuilder>
{
    protected TBuilder This => (TBuilder)this;

    public abstract T Build();

    public static implicit operator T(Builder<T, TBuilder> builder) => builder.Build();
}

// ============================================================================
// EXAMPLE: Generic Entity Builder
// This example shows the pattern - adapt to your actual domain types
// ============================================================================

/// <summary>
/// Example builder demonstrating the pattern with a simple Product entity.
/// Copy and adapt this for your domain entities.
/// </summary>
public sealed class ProductBuilder : Builder<Product, ProductBuilder>
{
    private Guid _id = Guid.NewGuid();
    private string _name = "Test Product";
    private string _sku = $"SKU-{Guid.NewGuid():N}"[..12];
    private decimal _price = 99.99m;
    private int _stock = 100;
    private bool _isActive = true;

    public ProductBuilder WithId(Guid id)
    {
        _id = id;
        return This;
    }

    public ProductBuilder WithName(string name)
    {
        _name = name;
        return This;
    }

    public ProductBuilder WithSku(string sku)
    {
        _sku = sku;
        return This;
    }

    public ProductBuilder WithPrice(decimal price)
    {
        _price = price;
        return This;
    }

    public ProductBuilder WithStock(int stock)
    {
        _stock = stock;
        return This;
    }

    public ProductBuilder Inactive()
    {
        _isActive = false;
        return This;
    }

    public override Product Build()
    {
        return new Product
        {
            Id = _id,
            Name = _name,
            Sku = _sku,
            Price = _price,
            Stock = _stock,
            IsActive = _isActive
        };
    }
}

/// <summary>
/// Simple Product class for the example builder.
/// Replace with your actual domain entity.
/// </summary>
public class Product
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Sku { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public int Stock { get; init; }
    public bool IsActive { get; init; }
}

// ============================================================================
// USAGE EXAMPLES
// ============================================================================

/*
// Basic usage - creates product with all defaults
var product = new ProductBuilder().Build();

// Fluent customization
var expensiveProduct = new ProductBuilder()
    .WithName("Premium Widget")
    .WithPrice(999.99m)
    .Build();

// Implicit conversion
Product inactiveProduct = new ProductBuilder().Inactive();

// In test methods
[Fact]
public void Order_CannotAddInactiveProduct()
{
    // Arrange
    var product = new ProductBuilder().Inactive().Build();
    var order = new OrderBuilder().Build();

    // Act
    var result = order.AddProduct(product);

    // Assert
    result.IsFailure.Should().BeTrue();
    result.Error.Message.Should().Contain("inactive");
}
*/
