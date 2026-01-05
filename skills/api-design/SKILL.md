---
name: api-design
description: API response patterns, pagination, filtering. Consistent API design conventions. Use when designing API endpoints or response structures.
allowed-tools: Read, Write, Edit, Glob, Grep
---

# API Design Patterns

Consistent patterns for API responses, pagination, filtering, and sorting.

**Source**: [Microsoft REST API Guidelines](https://github.com/microsoft/api-guidelines)

## Response Patterns

### Option 1: Direct Response (Simpler)

Return data directly without wrapper:

```csharp
// GET /orders/123
{
  "id": "123",
  "orderNumber": "ORD-001",
  "status": "Pending"
}

// GET /orders (collection)
[
  { "id": "123", "orderNumber": "ORD-001" },
  { "id": "124", "orderNumber": "ORD-002" }
]
```

### Option 2: Envelope Response (With Metadata)

Wrap data with metadata for pagination, etc.:

```csharp
// GET /orders?page=1&pageSize=10
{
  "data": [
    { "id": "123", "orderNumber": "ORD-001" }
  ],
  "meta": {
    "page": 1,
    "pageSize": 10,
    "totalCount": 100,
    "totalPages": 10,
    "hasNextPage": true,
    "hasPreviousPage": false
  }
}
```

## Pagination

### Offset-Based Pagination

Simple, supports jumping to pages. Less efficient for large datasets.

```csharp
// Application/Common/Pagination/PagedRequest.cs
public record PagedRequest(int Page = 1, int PageSize = 10)
{
    public int Skip => (Page - 1) * PageSize;
    public int Take => PageSize;
}

// Application/Common/Pagination/PagedResponse.cs
public sealed record PagedResponse<T>
{
    public IReadOnlyList<T> Data { get; init; } = [];
    public PageMeta Meta { get; init; } = new();
}

public sealed record PageMeta
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}
```

### Query Handler with Pagination

```csharp
public sealed class GetOrdersHandler(IDbContext db)
    : IQueryHandler<GetOrdersQuery, PagedResponse<OrderResponse>>
{
    public async Task<Result<PagedResponse<OrderResponse>>> HandleAsync(
        GetOrdersQuery query,
        CancellationToken ct)
    {
        var totalCount = await db.Orders
            .Where(o => o.Status == query.Status)
            .CountAsync(ct);

        var orders = await db.Orders
            .Where(o => o.Status == query.Status)
            .OrderByDescending(o => o.CreatedAt)
            .Skip(query.Skip)
            .Take(query.Take)
            .Select(o => new OrderResponse(o.Id, o.OrderNumber, o.Status))
            .ToListAsync(ct);

        return new PagedResponse<OrderResponse>
        {
            Data = orders,
            Meta = new PageMeta
            {
                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = totalCount
            }
        };
    }
}
```

### Cursor-Based Pagination

More efficient for large datasets. No page jumping.

```csharp
// Request
public record CursorRequest(string? Cursor = null, int Limit = 10);

// Response
public sealed record CursorResponse<T>
{
    public IReadOnlyList<T> Data { get; init; } = [];
    public string? NextCursor { get; init; }
    public bool HasMore { get; init; }
}

// Handler
public async Task<Result<CursorResponse<OrderResponse>>> HandleAsync(
    GetOrdersQuery query,
    CancellationToken ct)
{
    var ordersQuery = db.Orders.OrderByDescending(o => o.CreatedAt);

    if (query.Cursor is not null)
    {
        var cursorDate = DecodeCursor(query.Cursor);
        ordersQuery = ordersQuery.Where(o => o.CreatedAt < cursorDate);
    }

    var orders = await ordersQuery
        .Take(query.Limit + 1)  // Take one extra to check if more exist
        .Select(o => new OrderResponse(o.Id, o.OrderNumber, o.Status, o.CreatedAt))
        .ToListAsync(ct);

    var hasMore = orders.Count > query.Limit;
    var data = orders.Take(query.Limit).ToList();
    var nextCursor = hasMore ? EncodeCursor(data.Last().CreatedAt) : null;

    return new CursorResponse<OrderResponse>
    {
        Data = data,
        NextCursor = nextCursor,
        HasMore = hasMore
    };
}

private static string EncodeCursor(DateTimeOffset date) =>
    Convert.ToBase64String(Encoding.UTF8.GetBytes(date.ToString("O")));

private static DateTimeOffset DecodeCursor(string cursor) =>
    DateTimeOffset.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(cursor)));
```

## Filtering

### Query Parameters

```
GET /orders?status=Pending&customerId=123&createdAfter=2024-01-01
```

### Filter Object

```csharp
public sealed record GetOrdersQuery(
    int Page = 1,
    int PageSize = 10,
    OrderStatus? Status = null,
    Guid? CustomerId = null,
    DateTimeOffset? CreatedAfter = null,
    DateTimeOffset? CreatedBefore = null
) : IQuery<PagedResponse<OrderResponse>>
{
    public int Skip => (Page - 1) * PageSize;
    public int Take => PageSize;
}
```

### Applying Filters

```csharp
var query = db.Orders.AsQueryable();

if (request.Status.HasValue)
    query = query.Where(o => o.Status == request.Status.Value);

if (request.CustomerId.HasValue)
    query = query.Where(o => o.CustomerId == request.CustomerId.Value);

if (request.CreatedAfter.HasValue)
    query = query.Where(o => o.CreatedAt >= request.CreatedAfter.Value);

if (request.CreatedBefore.HasValue)
    query = query.Where(o => o.CreatedAt <= request.CreatedBefore.Value);
```

## Sorting

### Query Parameter

```
GET /orders?sortBy=createdAt&sortOrder=desc
```

### Sort Implementation

```csharp
public sealed record GetOrdersQuery(
    // ... pagination and filters
    string? SortBy = null,
    SortOrder SortOrder = SortOrder.Descending
) : IQuery<PagedResponse<OrderResponse>>;

public enum SortOrder { Ascending, Descending }

// Handler
var orderedQuery = query.SortBy?.ToLower() switch
{
    "ordernumber" => query.SortOrder == SortOrder.Ascending
        ? query.OrderBy(o => o.OrderNumber)
        : query.OrderByDescending(o => o.OrderNumber),
    "status" => query.SortOrder == SortOrder.Ascending
        ? query.OrderBy(o => o.Status)
        : query.OrderByDescending(o => o.Status),
    _ => query.SortOrder == SortOrder.Ascending
        ? query.OrderBy(o => o.CreatedAt)
        : query.OrderByDescending(o => o.CreatedAt)
};
```

## Endpoint Examples

### With Controllers

```csharp
[ApiController]
[Route("api/orders")]
public class OrdersController(
    IQueryHandler<GetOrdersQuery, PagedResponse<OrderResponse>> handler) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] OrderStatus? status = null,
        CancellationToken ct = default)
    {
        var query = new GetOrdersQuery(page, pageSize, status);
        var result = await handler.HandleAsync(query, ct);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.Error);
    }
}
```

### With Minimal APIs

```csharp
app.MapGet("/orders", async (
    [AsParameters] GetOrdersQuery query,
    IQueryHandler<GetOrdersQuery, PagedResponse<OrderResponse>> handler,
    CancellationToken ct) =>
{
    var result = await handler.HandleAsync(query, ct);
    return result.ToHttpResult();
});
```

## Best Practices

| Practice | Recommendation |
|----------|----------------|
| Default page size | 10-25 items |
| Maximum page size | 100 items (prevent abuse) |
| Collection URLs | Plural nouns (`/orders`, not `/order`) |
| Filters | Query parameters, not path segments |
| Sorting | Default to most useful order |
| Empty collections | Return `[]`, not 404 |

## Assets

- [assets/PagedResponse.cs](assets/PagedResponse.cs) - Pagination types

## Related

- `cqrs` - Query handlers
- `efcore` - Query performance
