# Integration Testing with WebApplicationFactory

Patterns for integration testing ASP.NET Core applications using WebApplicationFactory.

**Source**: [Integration tests in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests)

## Test Infrastructure

### Custom WebApplicationFactory

```csharp
// Tests/IntegrationTests/Infrastructure/CustomWebApplicationFactory.cs
namespace YourNamespace.Tests.IntegrationTests.Infrastructure;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YourNamespace.Infrastructure.Data;

public class CustomWebApplicationFactory<TProgram>
    : WebApplicationFactory<TProgram> where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Remove existing DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));

            if (descriptor != null)
                services.Remove(descriptor);

            // Add in-memory database for tests
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase($"TestDb-{Guid.NewGuid()}");
            });

            // Replace time provider with fake
            services.AddSingleton<TimeProvider>(new FakeTimeProvider(
                new DateTimeOffset(2024, 1, 15, 10, 0, 0, TimeSpan.Zero)));

            // Build the service provider
            var sp = services.BuildServiceProvider();

            // Create and seed the database
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();

            // Seed test data if needed
            SeedTestData(db);
        });
    }

    private static void SeedTestData(AppDbContext db)
    {
        // Add seed data for tests
        // Example:
        // db.Users.Add(TestData.DefaultUser);
        // db.SaveChanges();
    }
}
```

### Base Test Class

```csharp
// Tests/IntegrationTests/Infrastructure/IntegrationTestBase.cs
namespace YourNamespace.Tests.IntegrationTests.Infrastructure;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using YourNamespace.Api;
using YourNamespace.Infrastructure.Data;

[Collection("Integration")]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly CustomWebApplicationFactory<Program> Factory;
    protected readonly HttpClient Client;
    protected readonly IServiceScope Scope;
    protected readonly AppDbContext Db;

    protected IntegrationTestBase()
    {
        Factory = new CustomWebApplicationFactory<Program>();
        Client = Factory.CreateClient();
        Scope = Factory.Services.CreateScope();
        Db = Scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    public virtual Task InitializeAsync() => Task.CompletedTask;

    public virtual async Task DisposeAsync()
    {
        Scope.Dispose();
        await Factory.DisposeAsync();
    }

    protected void AuthenticateAs(string userId, params string[] roles)
    {
        // Add authentication header for test user
        var token = GenerateTestJwt(userId, roles);
        Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    private static string GenerateTestJwt(string userId, string[] roles)
    {
        // Generate a test JWT token
        // In practice, use a test token generator or mock authentication
        return "test-token";
    }
}
```

### Test Collection

```csharp
// Tests/IntegrationTests/Infrastructure/IntegrationTestCollection.cs
namespace YourNamespace.Tests.IntegrationTests.Infrastructure;

[CollectionDefinition("Integration")]
public class IntegrationTestCollection : ICollectionFixture<CustomWebApplicationFactory<Program>>
{
    // This class has no code, it's used to define the collection
}
```

## API Endpoint Tests

### Testing GET Endpoints

```csharp
// Tests/IntegrationTests/Orders/GetOrdersTests.cs
namespace YourNamespace.Tests.IntegrationTests.Orders;

using System.Net;
using System.Net.Http.Json;
using YourNamespace.Application.Orders.Queries;

public class GetOrdersTests : IntegrationTestBase
{
    [Fact]
    public async Task GetOrders_ReturnsOk_WhenAuthenticated()
    {
        // Arrange
        AuthenticateAs("user-123");

        // Act
        var response = await Client.GetAsync("/api/orders");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedResponse<OrderResponse>>();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetOrders_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        // Act
        var response = await Client.GetAsync("/api/orders");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetOrderById_ReturnsNotFound_WhenOrderDoesNotExist()
    {
        // Arrange
        AuthenticateAs("user-123");
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"/api/orders/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
```

### Testing POST Endpoints

```csharp
// Tests/IntegrationTests/Orders/CreateOrderTests.cs
namespace YourNamespace.Tests.IntegrationTests.Orders;

using System.Net;
using System.Net.Http.Json;
using YourNamespace.Application.Orders.Commands.CreateOrder;

public class CreateOrderTests : IntegrationTestBase
{
    [Fact]
    public async Task CreateOrder_ReturnsCreated_WithValidRequest()
    {
        // Arrange
        AuthenticateAs("user-123");
        var request = new CreateOrderRequest
        {
            CustomerId = Guid.NewGuid(),
            Items =
            [
                new OrderItemRequest { ProductId = Guid.NewGuid(), Quantity = 2 }
            ]
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/orders", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var orderId = await response.Content.ReadFromJsonAsync<Guid>();
        orderId.Should().NotBeEmpty();

        // Verify database
        var order = await Db.Orders.FindAsync(orderId);
        order.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateOrder_ReturnsBadRequest_WithInvalidRequest()
    {
        // Arrange
        AuthenticateAs("user-123");
        var request = new CreateOrderRequest
        {
            CustomerId = Guid.Empty, // Invalid
            Items = [] // Empty items
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/orders", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
```

## Authentication in Tests

### Mock Authentication Handler

```csharp
// Tests/IntegrationTests/Infrastructure/TestAuthHandler.cs
namespace YourNamespace.Tests.IntegrationTests.Infrastructure;

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";
    public const string UserIdHeader = "X-Test-UserId";
    public const string RolesHeader = "X-Test-Roles";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userId = Request.Headers[UserIdHeader].FirstOrDefault();

        if (string.IsNullOrEmpty(userId))
            return Task.FromResult(AuthenticateResult.Fail("No user ID provided"));

        var claims = new List<Claim>
        {
            new("sub", userId),
            new(ClaimTypes.NameIdentifier, userId)
        };

        var roles = Request.Headers[RolesHeader].FirstOrDefault()?.Split(',') ?? [];
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role.Trim()));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
```

### Register Test Authentication

```csharp
// In CustomWebApplicationFactory.ConfigureWebHost
builder.ConfigureTestServices(services =>
{
    services.AddAuthentication(TestAuthHandler.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
            TestAuthHandler.SchemeName, _ => { });
});
```

## Database Testing

### Using Real Database (TestContainers)

```csharp
// Tests/IntegrationTests/Infrastructure/DatabaseFixture.cs
namespace YourNamespace.Tests.IntegrationTests.Infrastructure;

using Testcontainers.MsSql;

public class DatabaseFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("Strong_password_123!")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
```

### Database Per Test Class

```csharp
public class OrderRepositoryTests : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private AppDbContext _db = null!;

    public OrderRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(_fixture.ConnectionString)
            .Options;

        _db = new AppDbContext(options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsOrder_WhenExists()
    {
        // Arrange
        var order = Order.Create(Guid.NewGuid(), "ORD-001").Value;
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        // Act
        var result = await _db.Orders.FindAsync(order.Id);

        // Assert
        result.Should().NotBeNull();
        result!.OrderNumber.Should().Be("ORD-001");
    }
}
```

## Respawn for Database Cleanup

```bash
dotnet add package Respawn
```

```csharp
// Tests/IntegrationTests/Infrastructure/DatabaseReset.cs
using Respawn;

public class IntegrationTestBase : IAsyncLifetime
{
    private static Respawner? _respawner;

    public async Task InitializeAsync()
    {
        _respawner ??= await Respawner.CreateAsync(ConnectionString, new RespawnerOptions
        {
            TablesToIgnore = new[] { new Table("__EFMigrationsHistory") },
            DbAdapter = DbAdapter.SqlServer
        });

        await _respawner.ResetAsync(ConnectionString);
    }
}
```

## Best Practices

| Practice | Recommendation |
|----------|----------------|
| Isolation | Each test should be independent and not affect others |
| Cleanup | Reset database state between tests |
| Authentication | Use test authentication handler for simplicity |
| Real database | Use TestContainers for production-like testing |
| Naming | Use descriptive names: Method_Scenario_ExpectedResult |
| Assertions | Use FluentAssertions for readable assertions |

## Related

- `testing` - Unit testing patterns
- `clean-architecture` - Test project structure
- `cqrs` - Testing handlers
