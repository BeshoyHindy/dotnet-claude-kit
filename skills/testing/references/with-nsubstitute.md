# Testing with NSubstitute

NSubstitute is a mocking library for .NET. Use it when you need to verify interactions or provide complex stub behavior.

## Basic Usage

```csharp
// Create substitute
var emailSender = Substitute.For<IEmailSender>();

// Configure return value
emailSender.SendAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>())
    .Returns(Task.CompletedTask);

// Use in test
var handler = new OrderHandler(emailSender);
await handler.HandleAsync(command, ct);

// Verify call
await emailSender.Received(1).SendAsync(
    Arg.Is<Email>(e => e.To == "customer@example.com"),
    Arg.Any<CancellationToken>());
```

## Creating Substitutes

```csharp
// Interface
var repository = Substitute.For<IOrderRepository>();

// Multiple interfaces
var combo = Substitute.For<IOrderRepository, IDisposable>();

// Class (must have virtual members)
var service = Substitute.For<OrderService>();

// With constructor args
var service = Substitute.For<OrderService>(dependency1, dependency2);
```

## Return Values

```csharp
// Simple return
repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
    .Returns(order);

// Async return
repository.GetByIdAsync(orderId, Arg.Any<CancellationToken>())
    .Returns(Task.FromResult<Order?>(order));

// Multiple calls return different values
repository.GetNextId().Returns(1, 2, 3);

// Return based on input
calculator.Add(Arg.Any<int>(), Arg.Any<int>())
    .Returns(args => (int)args[0] + (int)args[1]);

// Return for specific args
repository.GetByIdAsync(specificId, Arg.Any<CancellationToken>())
    .Returns(specificOrder);

// ReturnsNull
repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
    .ReturnsNull();
```

## Argument Matching

```csharp
// Any argument
Arg.Any<string>()
Arg.Any<CancellationToken>()

// Specific value
Arg.Is("exact string")
Arg.Is(42)

// Predicate
Arg.Is<Order>(o => o.Status == OrderStatus.Pending)
Arg.Is<string>(s => s.Contains("@"))

// Do-style matching (more readable for complex predicates)
emailSender.Received().SendAsync(
    Arg.Is<Email>(e =>
        e.To == "customer@example.com" &&
        e.Subject.Contains("Order Confirmed")),
    Arg.Any<CancellationToken>());
```

## Verifying Calls

```csharp
// Received exactly once
emailSender.Received(1).SendAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>());

// Received any number of times
emailSender.Received().SendAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>());

// Not received
emailSender.DidNotReceive().SendAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>());

// Received specific count
repository.Received(3).SaveAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());

// Check call order
Received.InOrder(() =>
{
    repository.GetByIdAsync(orderId, Arg.Any<CancellationToken>());
    repository.SaveAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
});
```

## Throwing Exceptions

```csharp
repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
    .ThrowsAsync(new InvalidOperationException("Database error"));

// Throw for specific conditions
repository.SaveAsync(Arg.Is<Order>(o => o.Items.Count == 0), Arg.Any<CancellationToken>())
    .ThrowsAsync<ValidationException>();
```

## Callbacks

```csharp
// Execute action when called
var savedOrders = new List<Order>();
repository.SaveAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>())
    .Returns(Task.CompletedTask)
    .AndDoes(args => savedOrders.Add(args.Arg<Order>()));

// When...Do syntax
repository.When(r => r.SaveAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>()))
    .Do(args => savedOrders.Add(args.Arg<Order>()));
```

## Properties

```csharp
var config = Substitute.For<IConfiguration>();

// Return value for property
config.ConnectionString.Returns("Server=test;Database=test");

// Verify property access
var _ = config.Received().ConnectionString;
```

## Events

```csharp
var eventSource = Substitute.For<IEventSource>();

// Raise event
eventSource.OrderCreated += Raise.EventWith(new OrderCreatedEventArgs(order));

// Raise with sender
eventSource.OrderCreated += Raise.EventWith(sender, new OrderCreatedEventArgs(order));
```

## Partial Substitutes

When you need real implementation for some methods:

```csharp
var service = Substitute.ForPartsOf<OrderService>(repository);

// Real method executes
service.CalculateTotal(order);

// Override specific method
service.GetDiscountPercentage(Arg.Any<Order>()).Returns(0.1m);
```

## Auto-Substitutes with AutoFixture

```csharp
public class OrderHandlerTests
{
    private readonly IFixture _fixture;
    private readonly IOrderRepository _repository;
    private readonly IEmailSender _emailSender;
    private readonly CreateOrderHandler _handler;

    public OrderHandlerTests()
    {
        _fixture = new Fixture().Customize(new AutoNSubstituteCustomization());
        _repository = _fixture.Freeze<IOrderRepository>();
        _emailSender = _fixture.Freeze<IEmailSender>();
        _handler = _fixture.Create<CreateOrderHandler>();
    }

    [Fact]
    public async Task Handle_CreatesOrder()
    {
        var command = _fixture.Create<CreateOrderCommand>();

        await _handler.HandleAsync(command, CancellationToken.None);

        await _repository.Received(1).SaveAsync(
            Arg.Any<Order>(),
            Arg.Any<CancellationToken>());
    }
}
```

## Best Practices

1. **Prefer fakes over mocks** for simple scenarios
2. **Don't mock what you don't own** - wrap external dependencies
3. **Verify only meaningful interactions** - don't over-specify
4. **Use Arg.Any** unless the specific value matters
5. **One mock verification per test** when possible
