using HydroExplorer.App.Services;
using HydroExplorer.Core.Data;
using HydroExplorer.Core.Hydrostatics;
using HydroExplorer.Core.Models;
using HydroExplorer.Core.Paging;

namespace HydroExplorer.App.Tests;

/// <summary>
/// In-memory stand-in for the REST API. It serves real data (from Core), records every request,
/// and can be told to fail or to hold responses until the test releases them.
/// </summary>
public sealed class FakeHydroApiClient : IHydroApiClient
{
    private readonly SeedHullRepository _repository = new();

    /// <summary>When true, every call throws <see cref="HydroApiException"/>, like the API being down.</summary>
    public bool IsDown { get; set; }

    /// <summary>
    /// When true, each page request waits until the test completes its entry in <see cref="HeldResponses"/>.
    /// This lets a test observe the loading state, or answer requests in a chosen order.
    /// </summary>
    public bool HoldResponses { get; set; }

    /// <summary>One entry per held page request, in request order. Call SetResult() to let it answer.</summary>
    public List<TaskCompletionSource> HeldResponses { get; } = new();

    /// <summary>Every page request received, in order, as (hullId, request).</summary>
    public List<(int HullId, HydrostaticsPageRequest Request)> PageRequests { get; } = new();

    /// <summary>Hull ids of every curve request received, in order.</summary>
    public List<int> CurveRequests { get; } = new();

    public Task<IReadOnlyList<Hull>> GetHullsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDown();
        return Task.FromResult(_repository.GetAll());
    }

    public async Task<PagedResult<HydrostaticRow>> GetHydrostaticsAsync(
        int hullId, HydrostaticsPageRequest request, CancellationToken cancellationToken = default)
    {
        PageRequests.Add((hullId, request));
        if (HoldResponses)
        {
            var held = new TaskCompletionSource();
            HeldResponses.Add(held);
            await held.Task;
        }

        ThrowIfDown();
        Hull hull = _repository.GetById(hullId) ?? throw new HydroApiException("The API returned 404 Not Found.");

        // Only draft sorting is needed by the tests. The real sorting is tested in the API tests.
        IEnumerable<HydrostaticRow> rows = HydrostaticTableGenerator.Generate(hull);
        if (request.SortDescending)
        {
            rows = rows.Reverse();
        }

        return Pager.Paginate(rows.ToList(), request.Page, request.PageSize);
    }

    public Task<IReadOnlyList<CurvePoint>> GetCurveAsync(int hullId, int points, CancellationToken cancellationToken = default)
    {
        CurveRequests.Add(hullId);
        ThrowIfDown();
        Hull hull = _repository.GetById(hullId) ?? throw new HydroApiException("The API returned 404 Not Found.");
        return Task.FromResult(HydrostaticCurveSampler.Sample(hull, points));
    }

    private void ThrowIfDown()
    {
        if (IsDown)
        {
            throw new HydroApiException("Cannot reach the API at http://localhost:5080/. Is it running?");
        }
    }
}
