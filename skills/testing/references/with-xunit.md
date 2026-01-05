# Testing with xUnit

xUnit is a popular testing framework for .NET. This reference covers xUnit-specific patterns.

## Basic Structure

```csharp
public class CalculatorTests
{
    [Fact]
    public void Add_TwoNumbers_ReturnsSum()
    {
        var calculator = new Calculator();

        var result = calculator.Add(2, 3);

        Assert.Equal(5, result);
    }
}
```

## Parameterized Tests

### InlineData

```csharp
[Theory]
[InlineData(1, 2, 3)]
[InlineData(0, 0, 0)]
[InlineData(-1, 1, 0)]
public void Add_VariousInputs_ReturnsCorrectSum(int a, int b, int expected)
{
    var calculator = new Calculator();

    var result = calculator.Add(a, b);

    Assert.Equal(expected, result);
}
```

### MemberData

```csharp
public class OrderValidationTests
{
    public static IEnumerable<object[]> InvalidOrders =>
    [
        [new Order { Items = [] }, "Order must have items"],
        [new Order { CustomerId = Guid.Empty }, "Customer is required"],
    ];

    [Theory]
    [MemberData(nameof(InvalidOrders))]
    public void Validate_InvalidOrder_ReturnsExpectedError(Order order, string expectedError)
    {
        var result = order.Validate();

        Assert.True(result.IsFailure);
        Assert.Contains(expectedError, result.Error.Message);
    }
}
```

### ClassData

```csharp
public class OrderTestData : IEnumerable<object[]>
{
    public IEnumerator<object[]> GetEnumerator()
    {
        yield return [CreatePendingOrder(), true];
        yield return [CreateShippedOrder(), false];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static Order CreatePendingOrder() => new() { Status = OrderStatus.Pending };
    private static Order CreateShippedOrder() => new() { Status = OrderStatus.Shipped };
}

[Theory]
[ClassData(typeof(OrderTestData))]
public void CanCancel_ReturnsExpectedResult(Order order, bool expected)
{
    Assert.Equal(expected, order.CanCancel);
}
```

## Lifecycle

### Constructor and Dispose

```csharp
public class DatabaseTests : IDisposable
{
    private readonly AppDbContext _db;

    public DatabaseTests()
    {
        // Runs before each test
        _db = CreateTestDatabase();
    }

    public void Dispose()
    {
        // Runs after each test
        _db.Dispose();
    }

    [Fact]
    public void Test1() { }

    [Fact]
    public void Test2() { }
}
```

### Async Lifecycle

```csharp
public class AsyncDatabaseTests : IAsyncLifetime
{
    private AppDbContext _db = null!;

    public async Task InitializeAsync()
    {
        _db = await CreateTestDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
    }
}
```

### Shared Context (Class Fixture)

```csharp
public class DatabaseFixture : IAsyncLifetime
{
    public AppDbContext Db { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Db = await CreateExpensiveDatabase();
    }

    public async Task DisposeAsync()
    {
        await Db.DisposeAsync();
    }
}

public class OrderRepositoryTests : IClassFixture<DatabaseFixture>
{
    private readonly AppDbContext _db;

    public OrderRepositoryTests(DatabaseFixture fixture)
    {
        _db = fixture.Db;
    }

    [Fact]
    public async Task Test1() { }

    [Fact]
    public async Task Test2() { }
}
```

### Collection Fixture

Share state across multiple test classes:

```csharp
[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture> { }

[Collection("Database")]
public class OrderTests
{
    public OrderTests(DatabaseFixture fixture) { }
}

[Collection("Database")]
public class CustomerTests
{
    public CustomerTests(DatabaseFixture fixture) { }
}
```

## Assertions

```csharp
// Equality
Assert.Equal(expected, actual);
Assert.NotEqual(unexpected, actual);

// Null
Assert.Null(value);
Assert.NotNull(value);

// Boolean
Assert.True(condition);
Assert.False(condition);

// Collections
Assert.Empty(collection);
Assert.Single(collection);
Assert.Contains(item, collection);
Assert.DoesNotContain(item, collection);
Assert.All(collection, item => Assert.True(item.IsValid));

// Strings
Assert.Contains("substring", actual);
Assert.StartsWith("prefix", actual);
Assert.EndsWith("suffix", actual);
Assert.Matches(@"\d+", actual);

// Types
Assert.IsType<Order>(result);
Assert.IsAssignableFrom<IEntity>(result);

// Exceptions
var ex = Assert.Throws<InvalidOperationException>(() => DoSomething());
Assert.Equal("Expected message", ex.Message);

var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await DoAsync());

// Ranges
Assert.InRange(value, low: 1, high: 100);
```

## Skipping Tests

```csharp
[Fact(Skip = "Not implemented yet")]
public void FutureFeature() { }

[Fact]
public void ConditionalSkip()
{
    Skip.If(Environment.OSVersion.Platform != PlatformID.Win32NT, "Windows only");
    // Test code
}
```

## Output

```csharp
public class OrderTests
{
    private readonly ITestOutputHelper _output;

    public OrderTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void TestWithOutput()
    {
        _output.WriteLine("Debugging information");
        _output.WriteLine($"Order ID: {order.Id}");
    }
}
```

## Configuration

xunit.runner.json:

```json
{
    "parallelizeTestCollections": true,
    "maxParallelThreads": 0,
    "methodDisplay": "method",
    "diagnosticMessages": true
}
```
