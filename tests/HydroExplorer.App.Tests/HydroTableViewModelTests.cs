using HydroExplorer.App.Services;
using HydroExplorer.App.ViewModels;

namespace HydroExplorer.App.Tests;

/// <summary>
/// ViewModel tests: no window, no HTTP. The fake client answers synchronously unless told to hold,
/// so after "await vm.LoadHullsAsync()" all follow-up loads have already finished.
/// </summary>
public class HydroTableViewModelTests
{
    // Small Barge (hull 1): 391 rows → 16 pages of 25 (the last has 16 rows).
    private readonly FakeHydroApiClient _api = new();
    private readonly HydroTableViewModel _vm;

    public HydroTableViewModelTests()
    {
        _vm = new HydroTableViewModel(_api, new SelectionService());
    }

    [Fact]
    public void BeforeLoading_ShowsNoRows()
    {
        Assert.Empty(_vm.Rows);
        Assert.Equal("No rows", _vm.PageSummary);
    }

    [Fact]
    public async Task LoadHulls_SelectsFirstHull_AndLoadsItsFirstPage()
    {
        await _vm.LoadHullsAsync();

        Assert.Equal(3, _vm.Hulls.Count);
        Assert.Equal(1, _vm.SelectedHull?.Id);
        Assert.Equal(25, _vm.Rows.Count);
        Assert.Equal(0.10, _vm.Rows[0].Draft);
        Assert.Equal("Page 1 of 16 (391 rows)", _vm.PageSummary);
        Assert.False(_vm.IsLoading);
        Assert.False(_vm.HasError);
    }

    // ---- Paging commands and CanExecute ----

    [Fact]
    public async Task OnFirstPage_CanOnlyGoForward()
    {
        await _vm.LoadHullsAsync();

        Assert.False(_vm.FirstPageCommand.CanExecute(null));
        Assert.False(_vm.PreviousPageCommand.CanExecute(null));
        Assert.True(_vm.NextPageCommand.CanExecute(null));
        Assert.True(_vm.LastPageCommand.CanExecute(null));
    }

    [Fact]
    public async Task NextPage_LoadsPage2()
    {
        await _vm.LoadHullsAsync();

        await _vm.NextPageCommand.ExecuteAsync(null);

        Assert.Equal(2, _vm.CurrentPage);
        Assert.Equal(0.35, _vm.Rows[0].Draft);
        Assert.True(_vm.PreviousPageCommand.CanExecute(null));
    }

    [Fact]
    public async Task LastPage_LoadsPartialLastPage_AndCanOnlyGoBack()
    {
        await _vm.LoadHullsAsync();

        await _vm.LastPageCommand.ExecuteAsync(null);

        Assert.Equal(16, _vm.CurrentPage);
        Assert.Equal(16, _vm.Rows.Count);
        Assert.Equal(4.00, _vm.Rows[^1].Draft);
        Assert.False(_vm.NextPageCommand.CanExecute(null));
        Assert.False(_vm.LastPageCommand.CanExecute(null));
        Assert.True(_vm.FirstPageCommand.CanExecute(null));
        Assert.True(_vm.PreviousPageCommand.CanExecute(null));
    }

    [Fact]
    public async Task PreviousAndFirstPage_GoBack()
    {
        await _vm.LoadHullsAsync();
        await _vm.LastPageCommand.ExecuteAsync(null);

        await _vm.PreviousPageCommand.ExecuteAsync(null);
        Assert.Equal(15, _vm.CurrentPage);

        await _vm.FirstPageCommand.ExecuteAsync(null);
        Assert.Equal(1, _vm.CurrentPage);
    }

    // ---- Hull, page size and sort changes ----

    [Fact]
    public async Task ChangingHull_ResetsToPage1_ForTheNewHull()
    {
        await _vm.LoadHullsAsync();
        await _vm.NextPageCommand.ExecuteAsync(null);

        _vm.SelectedHull = _vm.Hulls[2];  // Large Pontoon, 1191 rows

        Assert.Equal(1, _vm.CurrentPage);
        Assert.Equal((3, 1), (_api.PageRequests[^1].HullId, _api.PageRequests[^1].Request.Page));
        Assert.Equal(1191, _vm.TotalCount);
        Assert.Equal(48, _vm.TotalPages);
    }

    [Fact]
    public async Task ChangingPageSize_ResetsToPage1_WithNewSize()
    {
        await _vm.LoadHullsAsync();
        await _vm.NextPageCommand.ExecuteAsync(null);

        _vm.PageSize = 100;

        Assert.Equal(1, _vm.CurrentPage);
        Assert.Equal(100, _vm.Rows.Count);
        Assert.Equal(4, _vm.TotalPages);  // 391 / 100, rounded up
    }

    [Fact]
    public async Task SortingByCurrentColumn_TogglesDirection_AndReturnsToPage1()
    {
        await _vm.LoadHullsAsync();
        await _vm.NextPageCommand.ExecuteAsync(null);

        await _vm.SortByCommand.ExecuteAsync("draft");

        Assert.True(_vm.SortDescending);
        Assert.Equal(1, _vm.CurrentPage);
        Assert.Equal(4.00, _vm.Rows[0].Draft);
        Assert.True(_api.PageRequests[^1].Request.SortDescending);

        await _vm.SortByCommand.ExecuteAsync("draft");

        Assert.False(_vm.SortDescending);
    }

    [Fact]
    public async Task SortingByAnotherColumn_SortsAscendingByThatField()
    {
        await _vm.LoadHullsAsync();
        await _vm.SortByCommand.ExecuteAsync("draft");  // now descending

        await _vm.SortByCommand.ExecuteAsync("km");

        Assert.Equal("km", _vm.SortBy);
        Assert.False(_vm.SortDescending);
        Assert.Equal("km", _api.PageRequests[^1].Request.SortBy);
    }

    // ---- Loading and error states ----

    [Fact]
    public async Task WhileLoading_IsLoadingIsTrue_AndPagingIsDisabled()
    {
        await _vm.LoadHullsAsync();
        _api.HoldResponses = true;

        Task next = _vm.NextPageCommand.ExecuteAsync(null);

        Assert.True(_vm.IsLoading);
        Assert.False(_vm.NextPageCommand.CanExecute(null));
        Assert.False(_vm.RefreshCommand.CanExecute(null));

        _api.HeldResponses[0].SetResult();
        await next;

        Assert.False(_vm.IsLoading);
        Assert.Equal(2, _vm.CurrentPage);
    }

    [Fact]
    public async Task ApiDownAtStartup_ShowsError_AndRetryRecovers()
    {
        _api.IsDown = true;

        await _vm.LoadHullsAsync();

        Assert.True(_vm.HasError);
        Assert.Contains("Cannot reach the API", _vm.ErrorMessage);
        Assert.Empty(_vm.Hulls);
        Assert.True(_vm.RefreshCommand.CanExecute(null));

        _api.IsDown = false;
        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.False(_vm.HasError);
        Assert.Equal(3, _vm.Hulls.Count);
        Assert.Equal(25, _vm.Rows.Count);
    }

    [Fact]
    public async Task ApiDownDuringPaging_KeepsCurrentPage_AndRetryRecovers()
    {
        await _vm.LoadHullsAsync();
        _api.IsDown = true;

        await _vm.NextPageCommand.ExecuteAsync(null);

        Assert.True(_vm.HasError);
        Assert.Equal(1, _vm.CurrentPage);     // unchanged: the failed page never replaced it
        Assert.Equal(0.10, _vm.Rows[0].Draft);

        _api.IsDown = false;
        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.False(_vm.HasError);
        Assert.Equal(1, _vm.CurrentPage);
    }

    [Fact]
    public async Task StaleResponse_ArrivingAfterNewerOne_IsIgnored()
    {
        await _vm.LoadHullsAsync();
        _api.HoldResponses = true;

        Task older = _vm.LoadPageAsync(2);
        Task newer = _vm.LoadPageAsync(3);  // cancels the older request

        // Answer the newer request first, then the older one.
        _api.HeldResponses[1].SetResult();
        await newer;
        _api.HeldResponses[0].SetResult();
        await older;

        Assert.Equal(3, _vm.CurrentPage);  // the late page-2 response did not overwrite page 3
        Assert.Equal(0.60, _vm.Rows[0].Draft);
        Assert.False(_vm.IsLoading);
    }
}
