using HydroExplorer.App.Services;
using HydroExplorer.App.ViewModels;
using HydroExplorer.Core.Models;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;

namespace HydroExplorer.App.Tests;

/// <summary>
/// The table and chart ViewModels wired to one shared SelectionService, as in the app (minus the window).
/// </summary>
public class SelectionFlowTests
{
    private readonly FakeHydroApiClient _api = new();
    private readonly SelectionService _selection = new();
    private readonly HydroTableViewModel _table;
    private readonly HydroChartViewModel _chart;

    public SelectionFlowTests()
    {
        _table = new HydroTableViewModel(_api, _selection);
        _chart = new HydroChartViewModel(_api, _selection);
    }

    [Fact]
    public async Task SelectingFirstHull_LoadsItsCurve_WithNoMarker()
    {
        await _table.LoadHullsAsync();

        Assert.Equal(1, _selection.SelectedHull?.Id);
        Assert.Equal(new[] { 1 }, _api.CurveRequests);
        Assert.Equal(HydroChartViewModel.CurvePointCount, _chart.Curve.Count);
        Assert.Equal(4.0, _chart.Curve[^1].Draft);  // Small Barge depth
        Assert.Null(_chart.Marker);
    }

    [Fact]
    public async Task SelectingARow_MovesTheMarker_WithoutReloadingTheCurve()
    {
        await _table.LoadHullsAsync();
        HydrostaticRow row = _table.Rows[5];

        _table.SelectedRow = row;  // what the DataGrid does when the user clicks a row

        Assert.Equal(row, _selection.SelectedRow);
        Assert.Equal(new CurvePoint(row.Draft, row.Displacement), _chart.Marker);
        Assert.Equal($"T = {row.Draft:F2} m   Δ = {row.Displacement:N1} t", _chart.MarkerLabel);
        Assert.Single(_api.CurveRequests);  // still only the initial load
    }

    [Fact]
    public async Task ChangingHull_ClearsTheMarker_AndLoadsTheNewCurve()
    {
        await _table.LoadHullsAsync();
        _table.SelectedRow = _table.Rows[5];

        _table.SelectedHull = _table.Hulls[2];  // Large Pontoon

        Assert.Equal(3, _selection.SelectedHull?.Id);
        Assert.Null(_selection.SelectedRow);
        Assert.Null(_chart.Marker);
        Assert.Equal(new[] { 1, 3 }, _api.CurveRequests);
        Assert.Equal(12.0, _chart.Curve[^1].Draft);
    }

    [Fact]
    public async Task PagingAway_KeepsTheSelection_AndPagingBack_HighlightsTheRowAgain()
    {
        await _table.LoadHullsAsync();
        HydrostaticRow row = _table.Rows[5];
        _table.SelectedRow = row;

        await _table.NextPageCommand.ExecuteAsync(null);

        Assert.Null(_table.SelectedRow);           // the row isn't on page 2
        Assert.Equal(row, _selection.SelectedRow); // but it is still the shared selection
        Assert.NotNull(_chart.Marker);

        await _table.PreviousPageCommand.ExecuteAsync(null);

        Assert.Equal(row, _table.SelectedRow);
    }

    [Fact]
    public async Task ChartSeries_FollowTheViewModelState()
    {
        await _table.LoadHullsAsync();
        HydrostaticRow row = _table.Rows[5];

        _table.SelectedRow = row;

        var curveSeries = (LineSeries<ObservablePoint>)_chart.Series[0];
        var markerSeries = (ScatterSeries<ObservablePoint>)_chart.Series[1];
        Assert.Equal(HydroChartViewModel.CurvePointCount, curveSeries.Values!.Count());
        ObservablePoint marker = Assert.Single(markerSeries.Values!);
        Assert.Equal((row.Draft, row.Displacement), (marker.X, marker.Y));
        Assert.True(_chart.Sections[0].IsVisible);
        Assert.Equal(row.Draft, _chart.Sections[0].Xi);
    }

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
