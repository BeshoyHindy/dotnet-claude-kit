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
// EXAMPLE BUILDERS - Adapt to your domain types
// ============================================================================

/*
/// <summary>
/// Example Order builder for testing.
/// Demonstrates the builder pattern for creating test data.
/// </summary>
public sealed class OrderBuilder : Builder<Order, OrderBuilder>
{
    private Guid _id = Guid.NewGuid();
    private Guid _customerId = Guid.NewGuid();
    private string _orderNumber = $"ORD-{Guid.NewGuid():N}"[..14];
    private OrderStatus _status = OrderStatus.Draft;
    private readonly List<OrderItem> _items = [];

    public OrderBuilder WithId(Guid id)
    {
        _id = id;
        return This;
    }

    public OrderBuilder WithCustomer(Guid customerId)
    {
        _customerId = customerId;
        return This;
    }

    public OrderBuilder WithOrderNumber(string orderNumber)
    {
        _orderNumber = orderNumber;
        return This;
    }

    public OrderBuilder WithStatus(OrderStatus status)
    {
        _status = status;
        return This;
    }

    public OrderBuilder WithItem(Guid productId, int quantity, decimal unitPrice)
    {
        _items.Add(new OrderItem(productId, quantity, unitPrice));
        return This;
    }

    public override Order Build()
    {
        // Create order using domain factory
        var order = Order.Create(_customerId, _orderNumber).Value;

        foreach (var item in _items)
        {
            order.AddItem(item.ProductId, item.Quantity, item.UnitPrice);
        }

        // Set status if different from Draft (may need reflection for testing)
        if (_status != OrderStatus.Draft)
        {
            SetStatus(order, _status);
        }

        return order;
    }

    private static void SetStatus(Order order, OrderStatus status)
    {
        // Use reflection for testing purposes only
        var prop = typeof(Order).GetProperty(nameof(Order.Status));
        prop?.SetValue(order, status);
    }
}

/// <summary>
/// Example Customer builder for testing.
/// </summary>
public sealed class CustomerBuilder : Builder<Customer, CustomerBuilder>
{
    private Guid _id = Guid.NewGuid();
    private string _name = "Test Customer";
    private string _email = $"test-{Guid.NewGuid():N}@example.com"[..30];

    public CustomerBuilder WithId(Guid id)
    {
        _id = id;
        return This;
    }

    public CustomerBuilder WithName(string name)
    {
        _name = name;
        return This;
    }

    public CustomerBuilder WithEmail(string email)
    {
        _email = email;
        return This;
    }

    public override Customer Build()
    {
        return new Customer(_id, _name, _email);
    }
}
*/
