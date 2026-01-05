# Testing with Moq

Moq is a mocking library for .NET. Alternative to NSubstitute with similar capabilities.

## Installation

```bash
dotnet add package Moq
```

## Basic Usage

```csharp
// Create mock
var emailSender = new Mock<IEmailSender>();

// Configure return value
emailSender
    .Setup(x => x.SendAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
    .Returns(Task.CompletedTask);

// Use in test
var handler = new OrderHandler(emailSender.Object);
await handler.HandleAsync(command, ct);

// Verify call
emailSender.Verify(
    x => x.SendAsync(
        It.Is<Email>(e => e.To == "customer@example.com"),
        It.IsAny<CancellationToken>()),
    Times.Once);
```

## Creating Mocks

```csharp
// Interface
var repository = new Mock<IOrderRepository>();

// Strict mode (throws on unexpected calls)
var strict = new Mock<IOrderRepository>(MockBehavior.Strict);

// With default return values
var loose = new Mock<IOrderRepository>(MockBehavior.Loose);
```

## Return Values

```csharp
// Simple return
repository
    .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(order);

// Return null
repository
    .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync((Order?)null);

// Return based on input
calculator
    .Setup(c => c.Add(It.IsAny<int>(), It.IsAny<int>()))
    .Returns<int, int>((a, b) => a + b);

// Sequence of returns
repository.SetupSequence(r => r.GetNextId())
    .Returns(1)
    .Returns(2)
    .Returns(3);
```

## Argument Matching

```csharp
// Any value
It.IsAny<string>()
It.IsAny<CancellationToken>()

// Specific value
It.Is<Guid>(id => id == expectedId)

// Predicate
It.Is<Order>(o => o.Status == OrderStatus.Pending)

// Regex
It.IsRegex(@"^ORD-\d+$")

// Range
It.IsInRange(1, 100, Range.Inclusive)
```

## Verification

```csharp
// Called once
emailSender.Verify(
    x => x.SendAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()),
    Times.Once);

// Never called
emailSender.Verify(
    x => x.SendAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()),
    Times.Never);

// Called exactly N times
repository.Verify(
    r => r.SaveAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()),
    Times.Exactly(3));

// Called at least once
repository.Verify(
    r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
    Times.AtLeastOnce);

// Verify all setups were called
repository.VerifyAll();

// Verify no other calls
repository.VerifyNoOtherCalls();
```

## Throwing Exceptions

```csharp
repository
    .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
    .ThrowsAsync(new InvalidOperationException("Database error"));
```

## Callbacks

```csharp
var savedOrders = new List<Order>();

repository
    .Setup(r => r.SaveAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
    .Callback<Order, CancellationToken>((order, _) => savedOrders.Add(order))
    .Returns(Task.CompletedTask);
```

## Properties

```csharp
var config = new Mock<IConfiguration>();

// Property getter
config.Setup(c => c.ConnectionString).Returns("Server=test");

// Property with setter
config.SetupSet(c => c.Timeout = It.IsInRange(0, 60, Range.Inclusive));
```

## Protected Members

```csharp
var service = new Mock<OrderService>();

service.Protected()
    .Setup<decimal>("CalculateDiscount", ItExpr.IsAny<Order>())
    .Returns(0.1m);
```

## Comparison: Moq vs NSubstitute

| Feature | Moq | NSubstitute |
|---------|-----|-------------|
| Syntax | `mock.Setup()` / `mock.Object` | Direct substitute |
| Verification | `mock.Verify()` | `sub.Received()` |
| Any argument | `It.IsAny<T>()` | `Arg.Any<T>()` |
| Predicate | `It.Is<T>(pred)` | `Arg.Is<T>(pred)` |
| Strict mode | `MockBehavior.Strict` | Not built-in |
| LINQ support | `Mock.Of<T>()` | No |
