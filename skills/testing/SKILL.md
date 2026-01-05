---
name: testing
description: Testing patterns for .NET applications. Unit tests, integration tests, test organization. Use when writing tests or designing testable code.
allowed-tools: Read, Write, Edit, Glob, Grep, Bash
---

# Testing

**Source**: [Unit Testing Best Practices](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices) | [Integration Testing](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests)

Write tests that verify behavior, not implementation. Tests should be readable, maintainable, and provide confidence in changes.

## Test Organization

### Arrange-Act-Assert

```csharp
// Examples use xUnit syntax. See references/ for MSTest equivalents.
[Fact]
public async Task PlaceOrder_WithValidItems_CreatesOrder()
{
    // Arrange
    var customer = new Customer(Guid.NewGuid(), "Test Customer");
    var items = new[] { new OrderItem(productId, quantity: 2, price: 10.00m) };

    // Act
    var result = customer.PlaceOrder(items);

    // Assert
    Assert.True(result.IsSuccess);
    Assert.Single(customer.Orders);
}
```

### Test Naming

Name tests to describe the behavior:

```csharp
// Pattern: Method_Scenario_ExpectedResult
public void Cancel_WhenAlreadyShipped_ReturnsError()
public void Calculate_WithDiscount_AppliesPercentage()
public void Validate_MissingEmail_FailsValidation()
```

### Test Classes

One test class per class under test, grouped by method:

```csharp
public class OrderTests
{
    public class PlaceOrderTests
    {
        [Fact]
        public void WithValidItems_CreatesOrder() { }

        [Fact]
        public void WithEmptyItems_ReturnsValidationError() { }
    }

    public class CancelTests
    {
        [Fact]
        public void WhenPending_SetsStatusToCancelled() { }

        [Fact]
        public void WhenShipped_ReturnsError() { }
    }
}
```

## Unit Tests

### Testing Domain Logic

Domain entities should be tested without any infrastructure:

```csharp
public class OrderTests
{
    [Fact]
    public void AddItem_IncreasesTotalCorrectly()
    {
        var order = new Order(Guid.NewGuid());

        order.AddItem(productId: Guid.NewGuid(), quantity: 2, unitPrice: 25.00m);

        Assert.Equal(50.00m, order.Total.Amount);
    }

    [Fact]
    public void Cancel_WhenPending_SetsStatusToCancelled()
    {
        var order = new Order(Guid.NewGuid());

        var result = order.Cancel();

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void Cancel_WhenShipped_ReturnsError()
    {
        var order = new Order(Guid.NewGuid());
        order.Ship();

        var result = order.Cancel();

        Assert.True(result.IsFailure);
        Assert.Equal("Cannot cancel shipped order", result.Error.Message);
    }
}
```

### Testing with Dependencies

Use interfaces and test doubles:

```csharp
public class CreateOrderHandlerTests
{
    [Fact]
    public async Task Handle_WithValidCommand_CreatesAndSavesOrder()
    {
        // Arrange
        var repository = new FakeOrderRepository();
        var handler = new CreateOrderHandler(repository);
        var command = new CreateOrderCommand(customerId, items);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(repository.SavedOrders);
    }
}

// Simple fake for testing
public class FakeOrderRepository : IOrderRepository
{
    public List<Order> SavedOrders { get; } = [];

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken ct)
        => Task.FromResult(SavedOrders.FirstOrDefault(o => o.Id == id));

    public Task SaveAsync(Order order, CancellationToken ct)
    {
        SavedOrders.Add(order);
        return Task.CompletedTask;
    }
}
```

## Integration Tests

### Testing with Real Database

```csharp
// Use SQLite in-memory for realistic database behavior (not EF InMemory provider)
public class OrderRepositoryTests : IAsyncLifetime
{
    private AppDbContext _db = null!;
    private SqliteConnection _connection = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task Save_PersistsOrderWithItems()
    {
        var order = new Order(Guid.NewGuid());
        order.AddItem(productId, quantity: 1, price: 10.00m);

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        var saved = await _db.Orders
            .Include(o => o.Items)
            .FirstAsync(o => o.Id == order.Id);

        Assert.Single(saved.Items);
    }
}
```

### Testing API Endpoints

```csharp
public class OrdersApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public OrdersApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateOrder_ReturnsCreated()
    {
        var request = new CreateOrderRequest(customerId, items);

        var response = await _client.PostAsJsonAsync("/orders", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task GetOrder_WhenNotFound_Returns404()
    {
        var response = await _client.GetAsync($"/orders/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
```

## Test Doubles

### When to Use Each Type

| Type | Purpose | Example |
|------|---------|---------|
| Fake | Working implementation for testing | In-memory repository |
| Stub | Returns canned answers | Clock returning fixed time |
| Mock | Verifies interactions | Verify email was sent |
| Spy | Records calls for later verification | Capture logged messages |

### Simple Implementations

```csharp
// Fake - working implementation
public class FakeEmailSender : IEmailSender
{
    public List<Email> SentEmails { get; } = [];

    public Task SendAsync(Email email, CancellationToken ct)
    {
        SentEmails.Add(email);
        return Task.CompletedTask;
    }
}

// Stub - fixed time using TimeProvider (.NET 8+)
// Install: Microsoft.Extensions.TimeProvider.Testing
var fakeTime = new FakeTimeProvider(
    new DateTimeOffset(2024, 1, 15, 10, 0, 0, TimeSpan.Zero));

// Advance time in tests
fakeTime.Advance(TimeSpan.FromMinutes(30));
```

## Testing Result Pattern

```csharp
[Fact]
public void PlaceOrder_WithInvalidItems_ReturnsValidationError()
{
    var customer = new Customer(Guid.NewGuid(), "Test");

    var result = customer.PlaceOrder(items: []);

    Assert.True(result.IsFailure);
    Assert.Equal(ErrorType.Validation, result.Error.Type);
    Assert.Contains("items", result.Error.Message.ToLower());
}

[Fact]
public async Task Handle_WhenOrderNotFound_ReturnsNotFound()
{
    var handler = new CancelOrderHandler(new FakeOrderRepository());
    var command = new CancelOrderCommand(Guid.NewGuid());

    var result = await handler.HandleAsync(command, CancellationToken.None);

    Assert.True(result.IsFailure);
    Assert.Equal(ErrorType.NotFound, result.Error.Type);
}
```

## Test Data Builders

For complex object creation:

```csharp
public class OrderBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _customerId = Guid.NewGuid();
    private OrderStatus _status = OrderStatus.Pending;
    private readonly List<(Guid, int, decimal)> _items = [];

    public OrderBuilder WithId(Guid id) { _id = id; return this; }
    public OrderBuilder WithCustomer(Guid customerId) { _customerId = customerId; return this; }
    public OrderBuilder WithStatus(OrderStatus status) { _status = status; return this; }
    public OrderBuilder WithItem(Guid productId, int quantity, decimal price)
    {
        _items.Add((productId, quantity, price));
        return this;
    }

    public Order Build()
    {
        var order = new Order(_id, _customerId);
        foreach (var (productId, quantity, price) in _items)
        {
            order.AddItem(productId, quantity, price);
        }
        // Use reflection or internal setter for status if needed for testing
        return order;
    }
}

// Usage
var order = new OrderBuilder()
    .WithCustomer(customerId)
    .WithItem(productId, quantity: 2, price: 25.00m)
    .Build();
```

## References

Test Frameworks:
- [references/with-xunit.md](references/with-xunit.md) - xUnit test framework
- [references/with-mstest.md](references/with-mstest.md) - MSTest framework

Mocking Libraries:
- [references/with-nsubstitute.md](references/with-nsubstitute.md) - NSubstitute mocking
- [references/with-moq.md](references/with-moq.md) - Moq mocking

Assertion Libraries:
- [references/with-fluentassertions.md](references/with-fluentassertions.md) - FluentAssertions

## Assets

- [assets/TestDataBuilder.cs](assets/TestDataBuilder.cs) - Test data builder pattern

## Related

- `result-pattern` - Testing Result<T> returns
- `cqrs` - Testing handlers
- `efcore` - Integration test setup
