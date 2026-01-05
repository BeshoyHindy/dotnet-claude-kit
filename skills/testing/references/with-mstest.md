# Testing with MSTest

MSTest is Microsoft's testing framework, included with Visual Studio. Alternative to xUnit.

## Installation

```bash
dotnet add package MSTest.TestFramework
dotnet add package MSTest.TestAdapter
dotnet add package Microsoft.NET.Test.Sdk
```

## Basic Structure

```csharp
[TestClass]
public class CalculatorTests
{
    [TestMethod]
    public void Add_TwoNumbers_ReturnsSum()
    {
        var calculator = new Calculator();

        var result = calculator.Add(2, 3);

        Assert.AreEqual(5, result);
    }
}
```

## Parameterized Tests

### DataRow

```csharp
[TestMethod]
[DataRow(1, 2, 3)]
[DataRow(0, 0, 0)]
[DataRow(-1, 1, 0)]
public void Add_VariousInputs_ReturnsCorrectSum(int a, int b, int expected)
{
    var calculator = new Calculator();

    var result = calculator.Add(a, b);

    Assert.AreEqual(expected, result);
}
```

### DynamicData

```csharp
[TestClass]
public class OrderValidationTests
{
    public static IEnumerable<object[]> InvalidOrders
    {
        get
        {
            yield return new object[] { new Order { Items = new List<Item>() }, "Order must have items" };
            yield return new object[] { new Order { CustomerId = Guid.Empty }, "Customer is required" };
        }
    }

    [TestMethod]
    [DynamicData(nameof(InvalidOrders))]
    public void Validate_InvalidOrder_ReturnsExpectedError(Order order, string expectedError)
    {
        var result = order.Validate();

        Assert.IsFalse(result.IsSuccess);
        StringAssert.Contains(result.Error.Message, expectedError);
    }
}
```

## Lifecycle

```csharp
[TestClass]
public class DatabaseTests
{
    private AppDbContext _db = null!;

    [TestInitialize]
    public void Setup()
    {
        // Runs before each test
        _db = CreateTestDatabase();
    }

    [TestCleanup]
    public void Cleanup()
    {
        // Runs after each test
        _db.Dispose();
    }

    [ClassInitialize]
    public static void ClassSetup(TestContext context)
    {
        // Runs once before all tests in class
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        // Runs once after all tests in class
    }

    [TestMethod]
    public void Test1() { }

    [TestMethod]
    public void Test2() { }
}
```

## Async Tests

```csharp
[TestMethod]
public async Task GetOrder_WhenExists_ReturnsOrder()
{
    var repository = new OrderRepository(_db);

    var result = await repository.GetByIdAsync(orderId, CancellationToken.None);

    Assert.IsNotNull(result);
}
```

## Assertions

```csharp
// Equality
Assert.AreEqual(expected, actual);
Assert.AreNotEqual(unexpected, actual);

// Same reference
Assert.AreSame(expected, actual);
Assert.AreNotSame(unexpected, actual);

// Null
Assert.IsNull(value);
Assert.IsNotNull(value);

// Boolean
Assert.IsTrue(condition);
Assert.IsFalse(condition);

// Types
Assert.IsInstanceOfType(result, typeof(Order));
Assert.IsNotInstanceOfType(result, typeof(Customer));

// Strings
StringAssert.Contains(actual, "substring");
StringAssert.StartsWith(actual, "prefix");
StringAssert.EndsWith(actual, "suffix");
StringAssert.Matches(actual, new Regex(@"\d+"));

// Collections
CollectionAssert.Contains(collection, item);
CollectionAssert.DoesNotContain(collection, item);
CollectionAssert.AllItemsAreNotNull(collection);
CollectionAssert.AllItemsAreUnique(collection);
CollectionAssert.AreEqual(expected, actual);
CollectionAssert.AreEquivalent(expected, actual);

// Exceptions
Assert.ThrowsException<InvalidOperationException>(() => DoSomething());
await Assert.ThrowsExceptionAsync<ArgumentException>(async () => await DoAsync());
```

## Categories and Filtering

```csharp
[TestClass]
[TestCategory("Integration")]
public class IntegrationTests
{
    [TestMethod]
    [TestCategory("Slow")]
    public void SlowTest() { }

    [TestMethod]
    [TestCategory("Database")]
    public void DatabaseTest() { }
}

// Run specific category
// dotnet test --filter TestCategory=Integration
```

## Skipping Tests

```csharp
[TestMethod]
[Ignore("Not implemented yet")]
public void FutureFeature() { }

[TestMethod]
[Ignore]
public void TemporarilyDisabled() { }
```

## Test Context

```csharp
[TestClass]
public class OrderTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void TestWithContext()
    {
        TestContext.WriteLine("Debugging information");
        TestContext.WriteLine($"Test name: {TestContext.TestName}");
    }
}
```

## Comparison: MSTest vs xUnit

| Feature | MSTest | xUnit |
|---------|--------|-------|
| Test method | `[TestMethod]` | `[Fact]` |
| Test class | `[TestClass]` | Not required |
| Setup | `[TestInitialize]` | Constructor |
| Cleanup | `[TestCleanup]` | `IDisposable` |
| Parameterized | `[DataRow]` | `[InlineData]` |
| Shared fixture | `[ClassInitialize]` | `IClassFixture<T>` |
| Output | `TestContext.WriteLine` | `ITestOutputHelper` |
| Categories | `[TestCategory]` | `[Trait]` |
