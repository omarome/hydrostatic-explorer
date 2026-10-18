using HydroExplorer.App.Services;
using HydroExplorer.App.ViewModels;
using HydroExplorer.Core.Hydrostatics;
using HydroExplorer.Core.Models;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;

namespace HydroExplorer.App.Tests;

/// <summary>
/// The table, chart and hull ViewModels wired to one shared SelectionService, as in the app (minus the window).
/// Every view can change the selection; these tests check the other two follow.
/// </summary>
public class SelectionFlowTests
{
    // Small Barge (hull 1): 391 rows, 0.10 … 4.00 m. Page 1 (25 rows) holds 0.10 … 0.34 m.
    private readonly FakeHydroApiClient _api = new();
    private readonly SelectionService _selection = new();
    private readonly HydroTableViewModel _table;
    private readonly HydroChartViewModel _chart;
    private readonly HullViewModel _hull;

    public SelectionFlowTests()
    {
        _table = new HydroTableViewModel(_api, _selection, new FakeFileSaveService());
        _chart = new HydroChartViewModel(_api, _selection);
        _hull = new HullViewModel(_selection);
    }

    [Fact]
    public async Task SelectingFirstHull_LoadsItsCurve_WithNothingSelected()
    {
        await _table.LoadHullsAsync();

        Assert.Equal(1, _selection.SelectedHull?.Id);
        Assert.Equal(new[] { 1 }, _api.CurveRequests);
        Assert.Equal(HydroChartViewModel.CurvePointCount, _chart.Curve.Count);
        Assert.Null(_chart.Marker);
        Assert.Null(_hull.Draft);
    }

    // ---- Table → chart and hull ----

    [Fact]
    public async Task ClickingARow_MovesMarkerAndWaterline_WithoutAnyRequest()
    {
        await _table.LoadHullsAsync();
        HydrostaticRow row = _table.Rows[5];

        _table.SelectedRow = row;  // what the DataGrid does when the user clicks a row

        Assert.Equal(row.Draft, _selection.SelectedDraft);
        Assert.Equal(row.Draft, _chart.Marker?.Draft);
        Assert.Equal(row.Displacement, _chart.Marker!.Displacement, 1e-9);  // read off the curve
        Assert.Equal(row.Draft, _hull.Draft);
        Assert.Equal(row.Draft, _hull.SliderDraft);
        Assert.Single(_api.CurveRequests);
        Assert.Empty(_api.PageOfDraftRequests);
    }

    [Fact]
    public async Task ChangingHull_ClearsTheSelection_AndLoadsTheNewCurve()
    {
        await _table.LoadHullsAsync();
        _table.SelectedRow = _table.Rows[5];

        _table.SelectedHull = _table.Hulls[2];  // Large Pontoon

        Assert.Null(_selection.SelectedDraft);
        Assert.Null(_chart.Marker);
        Assert.Null(_hull.Draft);
        Assert.Equal(new[] { 1, 3 }, _api.CurveRequests);
        Assert.Equal(12.0, _hull.Depth);
    }

    [Fact]
    public async Task PagingAway_KeepsTheSelection_AndPagingBack_HighlightsTheRowAgain()
    {
        await _table.LoadHullsAsync();
        HydrostaticRow row = _table.Rows[5];
        _table.SelectedRow = row;

        await _table.NextPageCommand.ExecuteAsync(null);

        Assert.Null(_table.SelectedRow);                // the row isn't on page 2
        Assert.Equal(row.Draft, _selection.SelectedDraft);  // but it is still the shared selection
        Assert.NotNull(_chart.Marker);

        await _table.PreviousPageCommand.ExecuteAsync(null);

        Assert.Equal(row, _table.SelectedRow);
    }

    // ---- Chart → table and hull ----

    [Fact]
    public async Task ClickingTheChart_OnAnotherPage_JumpsTheTableToThatRow()
    {
        await _table.LoadHullsAsync();

        _chart.SelectDraftAt(3.2547);  // the chart's x-coordinate where the user clicked

        // Snapped to the nearest table draft; index 315 → page 13 of 25-row pages.
        Assert.Equal(3.25, _selection.SelectedDraft);
        Assert.Equal((1, 3.25), _api.PageOfDraftRequests.Single());
        Assert.Equal(13, _table.CurrentPage);
        Assert.Equal(3.25, _table.SelectedRow?.Draft);
        Assert.Equal(3.25, _chart.Marker?.Draft);
        Assert.Equal(3.25, _hull.Draft);
    }

    [Fact]
    public async Task ClickingTheChart_OnTheCurrentPage_SelectsTheRowWithoutARequest()
    {
        await _table.LoadHullsAsync();

        _chart.SelectDraftAt(0.2);

        Assert.Equal(0.2, _table.SelectedRow?.Draft);
        Assert.Empty(_api.PageOfDraftRequests);
    }

    [Fact]
    public async Task ChartMarker_AndSeries_FollowTheSelectedDraft()
    {
        await _table.LoadHullsAsync();

        _chart.SelectDraftAt(2.0);

        var markerSeries = (ScatterSeries<ObservablePoint>)_chart.Series[1];
        ObservablePoint marker = Assert.Single(markerSeries.Values!);
        Hull smallBarge = _table.Hulls[0];
        Assert.Equal(2.0, marker.X);
        Assert.Equal(HydrostaticsCalculator.Calculate(smallBarge, 2.0).Displacement, marker.Y!.Value, 1e-9);
        Assert.True(_chart.Sections[0].IsVisible);
        Assert.Equal(2.0, _chart.Sections[0].Xi);
    }

    // ---- Slider → table, chart and hull ----

    [Fact]
    public async Task MovingTheSlider_SelectsTheNearestDraft_InEveryView()
    {
        await _table.LoadHullsAsync();

        _hull.SliderDraft = 1.234;  // what the Slider's two-way binding does while the user drags

        Assert.Equal(1.23, _selection.SelectedDraft);
        Assert.Equal(1.23, _hull.Draft);
        Assert.Equal(1.23, _chart.Marker?.Draft);
        Assert.Equal(1.23, _table.SelectedRow?.Draft);
        Assert.Equal(5, _table.CurrentPage);  // index 113 → page 5
    }

    [Fact]
    public async Task SelectionFromElsewhere_MovesTheSlider_WithoutFeedback()
    {
        await _table.LoadHullsAsync();

        _table.SelectedRow = _table.Rows[3];

        Assert.Equal(_table.Rows[3].Draft, _hull.SliderDraft);
        Assert.Empty(_api.PageOfDraftRequests);  // the slider update didn't bounce back as a new selection
    }

    // Task.Run: inside it there is no SynchronizationContext, so when a held response is released,
    // the awaiting code continues immediately inside SetResult(). That makes the order of events
    // deterministic, with no sleeping and hoping.
    [Fact]
    public Task FastSliderDrag_OnlyTheLatestDraftsPageIsShown() => Task.Run(async () =>
    {
        await _table.LoadHullsAsync();
        _api.HoldResponses = true;

        _hull.SliderDraft = 2.0;  // request A (page 8)
        _hull.SliderDraft = 3.0;  // request B (page 12), cancels A

        _api.HeldResponses[1].SetResult();  // the newer answer arrives first...
        Assert.Equal(12, _table.CurrentPage);
        Assert.Equal(3.0, _table.SelectedRow?.Draft);

        _api.HeldResponses[0].SetResult();  // ...then the stale one, which must be ignored
        Assert.Equal(12, _table.CurrentPage);
        Assert.Equal(3.0, _table.SelectedRow?.Draft);
        Assert.Equal(3.0, _selection.SelectedDraft);
        Assert.False(_table.IsLoading);
    });

    [Fact]
    public Task PickingARowOnThisPage_WhileAnotherPageIsLoading_KeepsThisPage() => Task.Run(async () =>
    {
        await _table.LoadHullsAsync();
        _api.HoldResponses = true;
        _hull.SliderDraft = 3.0;            // starts loading page 12 for 3.00 m...

        _table.SelectedRow = _table.Rows[2];  // ...but the user clicks 0.12 m on page 1 meanwhile
        _api.HeldResponses[0].SetResult();    // the page-12 response arrives late

        Assert.Equal(1, _table.CurrentPage);
        Assert.Equal(0.12, _table.SelectedRow?.Draft);
        Assert.Equal(0.12, _hull.Draft);
        Assert.False(_table.IsLoading);
    });

    // ---- Keyboard ----

    [Fact]
    public async Task ArrowDown_AtLastRowOfPage_ContinuesOnNextPage()
    {
        await _table.LoadHullsAsync();
        _table.SelectedRow = _table.Rows[^1];  // 0.34 m, last row of page 1

        await _table.MoveSelectionAsync(+1);

        Assert.Equal(2, _table.CurrentPage);
        Assert.Equal(0.35, _table.SelectedRow?.Draft);
        Assert.Equal(0.35, _selection.SelectedDraft);
        Assert.Equal(0.35, _hull.Draft);
    }

    [Fact]
    public async Task ArrowUp_AtFirstRowOfPage_ContinuesOnPreviousPage()
    {
        await _table.LoadHullsAsync();
        await _table.NextPageCommand.ExecuteAsync(null);
        _table.SelectedRow = _table.Rows[0];  // 0.35 m

        await _table.MoveSelectionAsync(-1);

        Assert.Equal(1, _table.CurrentPage);
        Assert.Equal(0.34, _table.SelectedRow?.Draft);
    }

    [Fact]
    public async Task ArrowKeys_WithinPage_MoveOneRow_AndStopAtTableEnds()
    {
        await _table.LoadHullsAsync();

        await _table.MoveSelectionAsync(+1);  // nothing selected yet: starts at the first row
        Assert.Equal(0.10, _table.SelectedRow?.Draft);

        await _table.MoveSelectionAsync(+1);
        Assert.Equal(0.11, _table.SelectedRow?.Draft);

        await _table.MoveSelectionAsync(-1);
        await _table.MoveSelectionAsync(-1);  // already at the very first row: stays
        Assert.Equal(0.10, _table.SelectedRow?.Draft);
        Assert.Equal(1, _table.CurrentPage);
    }

    // ---- Errors ----

    [Fact]
    public async Task CurveLoadFailure_ShowsError_AndRetryRecovers()
    {
        _api.IsDown = true;
        _selection.SelectedHull = new Hull(1, "Small Barge", 40, 10, 4);

        Assert.True(_chart.HasError);
        Assert.Empty(_chart.Curve);

        _api.IsDown = false;
        await _chart.LoadCurveCommand.ExecuteAsync(null);

        Assert.False(_chart.HasError);
        Assert.Equal(HydroChartViewModel.CurvePointCount, _chart.Curve.Count);
    }
}
