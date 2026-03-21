namespace TaskTracker.Application.Common.Models;

/// <summary>
/// Standard pagination query parameters.
/// Applied to all list endpoints: GET /api/v1/tasks?page=1&amp;pageSize=20&amp;sortBy=createdAt&amp;sortDir=desc
/// </summary>
public record PagedQuery
{
    public int    Page     { get; init; } = 1;
    public int    PageSize { get; init; } = 20;
    public string SortBy   { get; init; } = "createdAt";
    public string SortDir  { get; init; } = "desc";
    public string? Search  { get; init; }

    /// <summary>Clamps page to 1+ and page size to 1–100 range.</summary>
    public PagedQuery Normalized() => this with
    {
        Page     = Math.Max(1, Page),
        PageSize = Math.Clamp(PageSize, 1, 100),
    };
}

/// <summary>
/// Standard paginated response envelope returned by all list endpoints.
/// Angular PagedResponse&lt;T&gt; interface maps to this shape directly.
/// </summary>
public record PagedResponse<T>
{
    public IReadOnlyList<T> Data            { get; init; } = [];
    public int              TotalCount      { get; init; }
    public int              Page            { get; init; }
    public int              PageSize        { get; init; }
    public int              TotalPages      => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool             HasNextPage     => Page < TotalPages;
    public bool             HasPreviousPage => Page > 1;

    public static PagedResponse<T> Create(IReadOnlyList<T> data, int totalCount, int page, int pageSize)
        => new() { Data = data, TotalCount = totalCount, Page = page, PageSize = pageSize };
}

/// <summary>
/// Uniform API response wrapper for single-item responses.
/// Angular services can always destructure { data, message, traceId }.
/// </summary>
public record ApiResponse<T>
{
    public T?     Data     { get; init; }
    public string? Message { get; init; }
    public bool   Success  { get; init; }

    public static ApiResponse<T> Ok(T data, string? message = null)
        => new() { Data = data, Success = true, Message = message };
}

/// <summary>Uniform error response shape — matches Angular error.interceptor expectations.</summary>
public record ErrorResponse(
    string  Error,
    string? Code    = null,
    string? TraceId = null,
    IDictionary<string, string[]>? Errors = null);
