using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using HydroExplorer.Core.Models;
using HydroExplorer.Core.Paging;

namespace HydroExplorer.App.Services;

/// <summary>
/// <see cref="IHydroApiClient"/> over HTTP. The HttpClient (with its BaseAddress) is created in App.axaml.cs.
/// Every request's outcome is reported to <see cref="ApiStatus"/> for the status bar.
/// </summary>
public sealed class HydroApiClient : IHydroApiClient
{
    private readonly HttpClient _http;
    private readonly ApiStatus _status;

    public HydroApiClient(HttpClient http, ApiStatus status)
    {
        _http = http;
        _status = status;
    }

    public Task<IReadOnlyList<Hull>> GetHullsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<Hull>>("api/hulls", cancellationToken);

    public Task<PagedResult<HydrostaticRow>> GetHydrostaticsAsync(
        int hullId, HydrostaticsPageRequest request, CancellationToken cancellationToken = default) =>
        GetAsync<PagedResult<HydrostaticRow>>(
            Url($"api/hulls/{hullId}/hydrostatics?page={request.Page}&{SortAndSize(request)}"),
            cancellationToken);

    public Task<PagedResult<HydrostaticRow>> GetPageContainingDraftAsync(
        int hullId, double draft, HydrostaticsPageRequest request, CancellationToken cancellationToken = default) =>
        GetAsync<PagedResult<HydrostaticRow>>(
            Url($"api/hulls/{hullId}/hydrostatics/page-of?draft={draft}&{SortAndSize(request)}"),
            cancellationToken);

    public Task<IReadOnlyList<CurvePoint>> GetCurveAsync(int hullId, int points, CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<CurvePoint>>(Url($"api/hulls/{hullId}/hydrostatics/curve?points={points}"), cancellationToken);

    private static string SortAndSize(HydrostaticsPageRequest request) =>
        Url($"pageSize={request.PageSize}&sortBy={request.SortBy}&sortDir={(request.SortDescending ? "desc" : "asc")}");

    /// <summary>
    /// Formats a URL with InvariantCulture, so numbers are always "3.25" and never "3,25"
    /// (which is what this machine's locale would produce).
    /// </summary>
    private static string Url(FormattableString url) => url.ToString(CultureInfo.InvariantCulture);

    /// <summary>GETs <paramref name="url"/>, reads the JSON body, and reports the outcome to <see cref="ApiStatus"/>.</summary>
    private async Task<T> GetAsync<T>(string url, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            T result = await SendAsync<T>(url, cancellationToken);
            _status.ReportSuccess(stopwatch.Elapsed);
            return result;
        }
        catch (HydroApiException ex)
        {
            _status.ReportFailure(ex.Message);
            throw;  // "throw;" rethrows the same exception, keeping its stack trace
        }
    }

    /// <summary>The HTTP call itself, turning every failure into a <see cref="HydroApiException"/>.</summary>
    private async Task<T> SendAsync<T>(string url, CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await _http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new HydroApiException($"The API returned {(int)response.StatusCode} {response.ReasonPhrase}.");
            }

            T? body = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
            return body ?? throw new HydroApiException("The API returned an empty response.");
        }
        catch (HttpRequestException ex)
        {
            // Connection refused, DNS failure, etc. Usually "the API isn't running".
            throw new HydroApiException($"Cannot reach the API at {_http.BaseAddress}. Is it running?", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient reports its own timeout as a cancellation. If *we* cancelled, let that propagate instead.
            throw new HydroApiException("The API did not respond in time.", ex);
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new HydroApiException("The API returned data in an unexpected format.", ex);
        }
    }
}
