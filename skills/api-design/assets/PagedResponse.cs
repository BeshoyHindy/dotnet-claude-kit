// Copy to: src/Application/Common/Pagination/*.cs (multiple files)
// Requires: None (pure C#)
// Application/Common/Pagination/PagedRequest.cs
namespace YourNamespace.Application.Common.Pagination;

public record PagedRequest(int Page = 1, int PageSize = 10)
{
    public int Skip => (Page - 1) * PageSize;
    public int Take => Math.Min(PageSize, MaxPageSize);

    private const int MaxPageSize = 100;
}

// Application/Common/Pagination/PagedResponse.cs
namespace YourNamespace.Application.Common.Pagination;

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
namespace YourNamespace.Application.Common.Pagination;

public record CursorRequest(string? Cursor = null, int Limit = 10)
{
    public int Take => Math.Min(Limit, MaxLimit);

    private const int MaxLimit = 100;
}

// Application/Common/Pagination/CursorResponse.cs
namespace YourNamespace.Application.Common.Pagination;

public sealed record CursorResponse<T>
{
    public IReadOnlyList<T> Data { get; init; } = [];
    public string? NextCursor { get; init; }
    public bool HasMore { get; init; }
}

// Application/Common/Pagination/CursorHelper.cs
namespace YourNamespace.Application.Common.Pagination;

using System.Text;

/// <summary>
/// Helper methods for encoding and decoding cursor-based pagination tokens.
/// </summary>
public static class CursorHelper
{
    public static string Encode(DateTimeOffset value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value.ToString("O")));

    public static string Encode(Guid value) =>
        Convert.ToBase64String(value.ToByteArray());

    /// <summary>
    /// Attempts to decode a cursor string to a DateTimeOffset.
    /// </summary>
    /// <returns>True if decoding succeeded; false if cursor was invalid.</returns>
    public static bool TryDecode(string? cursor, out DateTimeOffset value)
    {
        value = default;

        if (string.IsNullOrWhiteSpace(cursor))
            return false;

        try
        {
            var bytes = Convert.FromBase64String(cursor);
            var dateString = Encoding.UTF8.GetString(bytes);
            return DateTimeOffset.TryParse(dateString, out value);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// Attempts to decode a cursor string to a Guid.
    /// </summary>
    /// <returns>True if decoding succeeded; false if cursor was invalid.</returns>
    public static bool TryDecodeGuid(string? cursor, out Guid value)
    {
        value = default;

        if (string.IsNullOrWhiteSpace(cursor))
            return false;

        try
        {
            var bytes = Convert.FromBase64String(cursor);
            if (bytes.Length != 16)
                return false;

            value = new Guid(bytes);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// Decodes a cursor to DateTimeOffset. Throws if invalid.
    /// Prefer TryDecode for user input.
    /// </summary>
    public static DateTimeOffset Decode(string cursor) =>
        TryDecode(cursor, out var value)
            ? value
            : throw new ArgumentException("Invalid cursor format", nameof(cursor));

    /// <summary>
    /// Decodes a cursor to Guid. Throws if invalid.
    /// Prefer TryDecodeGuid for user input.
    /// </summary>
    public static Guid DecodeGuid(string cursor) =>
        TryDecodeGuid(cursor, out var value)
            ? value
            : throw new ArgumentException("Invalid cursor format", nameof(cursor));
}
