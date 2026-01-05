# Testing with FluentAssertions

FluentAssertions provides expressive assertions that work with any test framework (xUnit, MSTest, NUnit).

## Installation

```bash
dotnet add package FluentAssertions
```

## Basic Usage

```csharp
using FluentAssertions;

[Fact]
public void Order_WhenCreated_HasDraftStatus()
{
    var order = Order.Create(customerId, "ORD-001").Value;

    order.Status.Should().Be(OrderStatus.Draft);
}
```

## Object Assertions

```csharp
// Equality
result.Should().Be(expected);
result.Should().NotBe(unexpected);

// Null
result.Should().BeNull();
result.Should().NotBeNull();

// Type
result.Should().BeOfType<Order>();
result.Should().BeAssignableTo<IEntity>();

// Properties
order.Should().BeEquivalentTo(new { Id = orderId, Status = OrderStatus.Draft });
```

## String Assertions

```csharp
name.Should().Be("John");
name.Should().StartWith("Jo");
name.Should().EndWith("hn");
name.Should().Contain("oh");
name.Should().NotBeNullOrEmpty();
name.Should().NotBeNullOrWhiteSpace();
name.Should().HaveLength(4);
name.Should().MatchRegex(@"^[A-Z][a-z]+$");

// Case insensitive
name.Should().BeEquivalentTo("JOHN");
```

## Numeric Assertions

```csharp
count.Should().Be(5);
count.Should().BeGreaterThan(0);
count.Should().BeGreaterThanOrEqualTo(1);
count.Should().BeLessThan(100);
count.Should().BeInRange(1, 100);
count.Should().BePositive();

// Approximate
price.Should().BeApproximately(99.99m, 0.01m);
```

## Collection Assertions

```csharp
// Count
items.Should().HaveCount(3);
items.Should().BeEmpty();
items.Should().NotBeEmpty();
items.Should().ContainSingle();

// Contains
items.Should().Contain(specificItem);
items.Should().Contain(i => i.Name == "Widget");
items.Should().NotContain(specificItem);

// All items
items.Should().OnlyContain(i => i.IsValid);
items.Should().AllBeOfType<OrderItem>();
items.Should().AllSatisfy(i => i.Quantity.Should().BePositive());

// Order
items.Should().BeInAscendingOrder(i => i.Name);
items.Should().BeInDescendingOrder(i => i.Price);

// Equivalence
items.Should().BeEquivalentTo(expectedItems);
items.Should().BeEquivalentTo(expectedItems,
    options => options.WithStrictOrdering());
```

## DateTime Assertions

```csharp
date.Should().Be(expected);
date.Should().BeAfter(startDate);
date.Should().BeBefore(endDate);
date.Should().BeOnOrAfter(startDate);
date.Should().BeCloseTo(expected, TimeSpan.FromSeconds(1));
date.Should().HaveYear(2024);
date.Should().HaveMonth(6);
date.Should().HaveDay(15);
```

## Exception Assertions

```csharp
// Sync
Action act = () => order.Cancel();
act.Should().Throw<InvalidOperationException>()
    .WithMessage("Cannot cancel*");

act.Should().NotThrow();

// Async
Func<Task> act = () => handler.HandleAsync(command, ct);
await act.Should().ThrowAsync<InvalidOperationException>();
await act.Should().NotThrowAsync();
```

## Result Pattern Assertions

Custom extensions for Result<T>:

```csharp
public static class ResultAssertions
{
    public static void BeSuccess<T>(this ObjectAssertions assertions)
    {
        var result = assertions.Subject as Result<T>;
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeTrue($"expected success but got error: {result.Error?.Message}");
    }

    public static void BeFailure<T>(this ObjectAssertions assertions)
    {
        var result = assertions.Subject as Result<T>;
        result.Should().NotBeNull();
        result!.IsFailure.Should().BeTrue("expected failure but got success");
    }

    public static void HaveError<T>(this ObjectAssertions assertions, string containing)
    {
        var result = assertions.Subject as Result<T>;
        result.Should().NotBeNull();
        result!.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain(containing);
    }
}

// Usage
var result = Order.Create(Guid.Empty, "ORD-001");
result.Should().BeFailure<Order>();
result.Should().HaveError<Order>("Customer");
```

## Execution Time

```csharp
Action act = () => ExpensiveOperation();
act.ExecutionTime().Should().BeLessThan(TimeSpan.FromSeconds(1));
```

## Chaining Assertions

```csharp
order.Should().NotBeNull()
    .And.Subject.Status.Should().Be(OrderStatus.Draft);

items.Should().NotBeEmpty()
    .And.HaveCount(3)
    .And.OnlyContain(i => i.Quantity > 0);
```

## Equivalency Options

```csharp
actual.Should().BeEquivalentTo(expected, options => options
    .Excluding(o => o.Id)
    .Excluding(o => o.CreatedAt)
    .WithStrictOrdering());

actual.Should().BeEquivalentTo(expected, options => options
    .Including(o => o.Name)
    .Including(o => o.Email));

actual.Should().BeEquivalentTo(expected, options => options
    .ComparingByMembers<Order>()
    .IgnoringCyclicReferences());
```
