using HydroExplorer.Core.Models;
using HydroExplorer.Core.Paging;

namespace HydroExplorer.App.Services;

/// <summary>
/// Everything the app needs from the REST API. ViewModels depend on this interface, not on
/// HttpClient, so tests can pass in a fake (like mocking a fetch wrapper in React tests).
/// </summary>
/// <remarks>Every method throws <see cref="HydroApiException"/> when the API can't be reached or returns an error.</remarks>
public interface IHydroApiClient
{
    Task<IReadOnlyList<Hull>> GetHullsAsync(CancellationToken cancellationToken = default);

    Task<PagedResult<HydrostaticRow>> GetHydrostaticsAsync(
        int hullId, HydrostaticsPageRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Which page of the hydrostatic table to fetch, and in what order.</summary>
/// <param name="SortBy">"draft", "displacement" or "km".</param>
public sealed record HydrostaticsPageRequest(int Page, int PageSize, string SortBy, bool SortDescending);

/// <summary>The API is down, timed out, or returned an error. The message is shown to the user.</summary>
public sealed class HydroApiException : Exception
{
    public HydroApiException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
