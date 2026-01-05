// Application/Common/Pagination/PagedRequest.cs
namespace YourApp.Application.Common.Pagination;

public record PagedRequest(int Page = 1, int PageSize = 10)
{
    public int Skip => (Page - 1) * PageSize;
    public int Take => Math.Min(PageSize, MaxPageSize);

    private const int MaxPageSize = 100;
}

// Application/Common/Pagination/PagedResponse.cs
namespace YourApp.Application.Common.Pagination;

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
    public int TotalPages => PageSize > 0
        ? (int)Math.Ceiling(TotalCount / (double)PageSize)
        : 0;
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

// Application/Common/Pagination/CursorRequest.cs
namespace YourApp.Application.Common.Pagination;

public record CursorRequest(string? Cursor = null, int Limit = 10)
{
    public int Take => Math.Min(Limit, MaxLimit);

    private const int MaxLimit = 100;
}

// Application/Common/Pagination/CursorResponse.cs
namespace YourApp.Application.Common.Pagination;

public sealed record CursorResponse<T>
{
    public IReadOnlyList<T> Data { get; init; } = [];
    public string? NextCursor { get; init; }
    public bool HasMore { get; init; }
}

// Application/Common/Pagination/CursorHelper.cs
namespace YourApp.Application.Common.Pagination;

using System.Text;

public static class CursorHelper
{
    public static string Encode(DateTimeOffset value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value.ToString("O")));

    public static DateTimeOffset Decode(string cursor) =>
        DateTimeOffset.Parse(
            Encoding.UTF8.GetString(Convert.FromBase64String(cursor)));

    public static string Encode(Guid value) =>
        Convert.ToBase64String(value.ToByteArray());

    public static Guid DecodeGuid(string cursor) =>
        new(Convert.FromBase64String(cursor));
}
