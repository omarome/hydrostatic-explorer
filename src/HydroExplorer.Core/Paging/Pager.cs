namespace HydroExplorer.Core.Paging;

/// <summary>Paging math, kept separate so it can be tested on its own.</summary>
public static class Pager
{
    /// <summary>Number of pages needed for <paramref name="totalCount"/> items (rounds up).</summary>
    public static int TotalPages(int totalCount, int pageSize)
    {
        RequireValidPageSize(pageSize);
        // Integer "ceiling division": e.g. 1191 items / 25 per page = 48 pages (the last one partial).
        return (totalCount + pageSize - 1) / pageSize;
    }

    /// <summary>
    /// Returns page <paramref name="page"/> (1-based) of <paramref name="source"/>.
    /// A page past the end returns no items but correct totals, so a client can recover.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="page"/> or <paramref name="pageSize"/> is less than 1.</exception>
    public static PagedResult<T> Paginate<T>(IReadOnlyList<T> source, int page, int pageSize)
    {
        ArgumentNullException.ThrowIfNull(source);
        RequireValidPageSize(pageSize);
        if (page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(page), page, "Page numbers start at 1.");
        }

        int totalCount = source.Count;

        // Skip/Take are LINQ, the C# equivalent of array.slice(start, start + count) in JS.
        // "long" guards against int overflow when page * pageSize is huge.
        long skip = (long)(page - 1) * pageSize;
        List<T> items = skip >= totalCount
            ? new List<T>()
            : source.Skip((int)skip).Take(pageSize).ToList();

        return new PagedResult<T>(items, page, pageSize, totalCount, TotalPages(totalCount, pageSize));
    }

    /// <summary>The 1-based page that contains the item at zero-based <paramref name="index"/>.</summary>
    public static int PageOfIndex(int index, int pageSize)
    {
        RequireValidPageSize(pageSize);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        return index / pageSize + 1;
    }

    private static void RequireValidPageSize(int pageSize)
    {
        if (pageSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, "Page size must be at least 1.");
        }
    }
}
