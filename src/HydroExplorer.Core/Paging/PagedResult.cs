namespace HydroExplorer.Core.Paging;

/// <summary>
/// One page of a larger result set, plus the metadata a client needs to show "Page X of Y".
/// Shared by the API (which returns it as JSON) and the app (which reads it back).
/// </summary>
/// <typeparam name="T">Item type. Generics work like TypeScript's <c>PagedResult&lt;T&gt;</c>.</typeparam>
/// <param name="Items">The items on this page. Empty if <paramref name="Page"/> is past the last page.</param>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Maximum items per page.</param>
/// <param name="TotalCount">Number of items across all pages.</param>
/// <param name="TotalPages">Number of pages; 0 when there are no items.</param>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
