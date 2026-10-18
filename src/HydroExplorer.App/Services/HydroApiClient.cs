using System.Globalization;
using System.Net.Http.Json;
using HydroExplorer.Core.Models;
using HydroExplorer.Core.Paging;

namespace HydroExplorer.App.Services;

/// <summary>
/// <see cref="IHydroApiClient"/> over HTTP. The HttpClient (with its BaseAddress) is created in App.axaml.cs.
/// </summary>
public sealed class HydroApiClient : IHydroApiClient
{
    private readonly HttpClient _http;

    public HydroApiClient(HttpClient http)
    {
        _http = http;
    }

    public Task<IReadOnlyList<Hull>> GetHullsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<Hull>>("api/hulls", cancellationToken);

    public Task<PagedResult<HydrostaticRow>> GetHydrostaticsAsync(
        int hullId, HydrostaticsPageRequest request, CancellationToken cancellationToken = default)
    {
        // InvariantCulture so numbers are always written as "25", never in a locale-specific format.
        string url = string.Create(
            CultureInfo.InvariantCulture,
            $"api/hulls/{hullId}/hydrostatics?page={request.Page}&pageSize={request.PageSize}" +
            $"&sortBy={request.SortBy}&sortDir={(request.SortDescending ? "desc" : "asc")}");
        return GetAsync<PagedResult<HydrostaticRow>>(url, cancellationToken);
    }

    /// <summary>GETs <paramref name="url"/> and reads the JSON body, turning every failure into a <see cref="HydroApiException"/>.</summary>
    private async Task<T> GetAsync<T>(string url, CancellationToken cancellationToken)
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
