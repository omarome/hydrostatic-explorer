using HydroExplorer.Core.Models;
using HydroExplorer.Core.Paging;

namespace HydroExplorer.Api.Queries;

/// <summary>
/// The query-string options for GET /api/hulls/{id}/hydrostatics: validation, then filter → sort → page.
/// </summary>
public sealed record HydrostaticsQuery(
    int Page,
    int PageSize,
    string SortBy,
    string SortDir,
    double? MinDraft,
    double? MaxDraft)
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 200;

    /// <summary>Allowed values for sortBy (compared case-insensitively).</summary>
    public static readonly IReadOnlyList<string> SortFields = new[] { "draft", "displacement", "km" };

    /// <summary>Allowed values for sortDir (compared case-insensitively).</summary>
    public static readonly IReadOnlyList<string> SortDirections = new[] { "asc", "desc" };

    /// <summary>
    /// Returns validation errors keyed by query parameter name, or an empty dictionary if the query is valid.
    /// This shape is what <c>Results.ValidationProblem</c> expects for a 400 ProblemDetails response.
    /// </summary>
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();

        if (Page < 1)
        {
            errors["page"] = new[] { "page must be 1 or greater." };
        }

        if (PageSize < 1 || PageSize > MaxPageSize)
        {
            errors["pageSize"] = new[] { $"pageSize must be between 1 and {MaxPageSize}." };
        }

        if (!SortFields.Contains(SortBy, StringComparer.OrdinalIgnoreCase))
        {
            errors["sortBy"] = new[] { $"sortBy must be one of: {string.Join(", ", SortFields)}." };
        }

        if (!SortDirections.Contains(SortDir, StringComparer.OrdinalIgnoreCase))
        {
            errors["sortDir"] = new[] { "sortDir must be asc or desc." };
        }

        if (MinDraft is < 0)
        {
            errors["minDraft"] = new[] { "minDraft must not be negative." };
        }

        if (MaxDraft is < 0)
        {
            errors["maxDraft"] = new[] { "maxDraft must not be negative." };
        }

        if (MinDraft is not null && MaxDraft is not null && MinDraft > MaxDraft)
        {
            errors["minDraft"] = new[] { "minDraft must not be greater than maxDraft." };
        }

        return errors;
    }

    /// <summary>Filters, sorts and pages <paramref name="rows"/>. Call <see cref="Validate"/> first.</summary>
    public PagedResult<HydrostaticRow> Apply(IEnumerable<HydrostaticRow> rows) =>
        Pager.Paginate(FilterAndSort(rows), Page, PageSize);

    /// <summary>
    /// Returns the page (of <see cref="PageSize"/> rows, in this query's sort order) that contains the row
    /// with exactly <paramref name="draft"/>, or null if no row has that draft. Page and draft filter are ignored.
    /// </summary>
    public PagedResult<HydrostaticRow>? PageContaining(IEnumerable<HydrostaticRow> rows, double draft)
    {
        List<HydrostaticRow> sorted = (this with { MinDraft = null, MaxDraft = null }).FilterAndSort(rows);
        int index = sorted.FindIndex(row => row.Draft == draft);
        return index < 0 ? null : Pager.Paginate(sorted, Pager.PageOfIndex(index, PageSize), PageSize);
    }

    private List<HydrostaticRow> FilterAndSort(IEnumerable<HydrostaticRow> rows)
    {
        // 1. Filter by the optional draft range. LINQ's Where is like Array.prototype.filter.
        IEnumerable<HydrostaticRow> filtered = rows;
        if (MinDraft is not null)
        {
            filtered = filtered.Where(row => row.Draft >= MinDraft);
        }

        if (MaxDraft is not null)
        {
            filtered = filtered.Where(row => row.Draft <= MaxDraft);
        }

        // 2. Sort. ThenBy(draft) makes the order fully defined when two rows have the same key.
        Func<HydrostaticRow, double> key = SortKey(SortBy);
        bool descending = SortDir.Equals("desc", StringComparison.OrdinalIgnoreCase);
        IOrderedEnumerable<HydrostaticRow> sorted = descending
            ? filtered.OrderByDescending(key).ThenByDescending(row => row.Draft)
            : filtered.OrderBy(key).ThenBy(row => row.Draft);

        return sorted.ToList();
    }

    // A switch expression maps each allowed sortBy value to "which property to sort on".
    private static Func<HydrostaticRow, double> SortKey(string sortBy) => sortBy.ToLowerInvariant() switch
    {
        "draft" => row => row.Draft,
        "displacement" => row => row.Displacement,
        "km" => row => row.Km,
        _ => throw new ArgumentOutOfRangeException(nameof(sortBy), sortBy, "Unknown sort field."),
    };
}
