using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HydroExplorer.App.Services;
using HydroExplorer.Core.Models;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using SkiaSharp;

namespace HydroExplorer.App.ViewModels;

/// <summary>
/// The displacement-vs-draft chart. It follows the shared selection: a new hull reloads the curve
/// from the API, and a new row only moves the marker (no request).
/// </summary>
/// <remarks>
/// The plain data (<see cref="Curve"/>, <see cref="Marker"/>) is what tests check. The LiveCharts
/// objects (<see cref="Series"/>, axes, sections) are created once and updated from that data.
/// </remarks>
public partial class HydroChartViewModel : ViewModelBase
{
    /// <summary>Number of curve points requested from the API. Plenty for a smooth line.</summary>
    public const int CurvePointCount = 100;

    private static readonly SKColor CurveColor = SKColor.Parse("#1E88E5");
    private static readonly SKColor MarkerColor = SKColor.Parse("#FB8C00");
    private static readonly SKColor AxisColor = SKColor.Parse("#8A8A8A");

    private readonly IHydroApiClient _api;
    private readonly SelectionService _selection;
    private readonly LineSeries<ObservablePoint> _curveSeries;
    private readonly ScatterSeries<ObservablePoint> _markerSeries;
    private readonly RectangularSection _markerLine;
    private CancellationTokenSource? _curveLoadCancellation;

    public HydroChartViewModel(IHydroApiClient api, SelectionService selection)
    {
        _api = api;
        _selection = selection;

        _curveSeries = new LineSeries<ObservablePoint>
        {
            Name = "Displacement",
            Values = Array.Empty<ObservablePoint>(),
            Stroke = new SolidColorPaint(CurveColor, 2),
            Fill = null,             // no shaded area under the line
            GeometrySize = 0,        // no dot on every point
            LineSmoothness = 0,      // straight segments between points
        };

        _markerSeries = new ScatterSeries<ObservablePoint>
        {
            Name = "Selected draft",
            Values = Array.Empty<ObservablePoint>(),
            GeometrySize = 14,
            Fill = new SolidColorPaint(MarkerColor),
            Stroke = new SolidColorPaint(SKColors.White, 2),
        };

        // A section with Xi == Xj draws a vertical line: here a dashed line at the selected draft.
        _markerLine = new RectangularSection
        {
            IsVisible = false,
            Stroke = new SolidColorPaint(MarkerColor, 1) { PathEffect = new DashEffect(new float[] { 4, 4 }) },
        };

        Series = new ISeries[] { _curveSeries, _markerSeries };
        Sections = new[] { _markerLine };
        XAxes = new[] { CreateAxis("Draft T (m)", value => value.ToString("0.##")) };
        YAxes = new[] { CreateAxis("Displacement Δ (t)", value => value.ToString("N0")) };

        // Subscribe to the shared selection. Both objects are app-lifetime singletons, so this
        // subscription never needs removing (see DECISIONS.md, Phase 4).
        _selection.PropertyChanged += OnSelectionChanged;
    }

    // ---- Plain state (bound by the view, checked by tests) ----

    [ObservableProperty]
    private IReadOnlyList<CurvePoint> _curve = Array.Empty<CurvePoint>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MarkerLabel))]
    private CurvePoint? _marker;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadCurveCommand))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    public bool HasError => ErrorMessage is not null;

    /// <summary>For example "T = 3.25 m   Δ = 11,070.0 t".</summary>
    public string MarkerLabel => Marker is null
        ? "Select a row to mark its draft"
        : $"T = {Marker.Draft:F2} m   Δ = {Marker.Displacement:N1} t";

    // ---- LiveCharts objects (bound by CartesianChart in HydroChartView.axaml) ----

    public ISeries[] Series { get; }

    public RectangularSection[] Sections { get; }

    public Axis[] XAxes { get; }

    public Axis[] YAxes { get; }

    // ---- Loading ----

    /// <summary>Loads the curve for the selected hull. Never throws: errors go to <see cref="ErrorMessage"/>.</summary>
    [RelayCommand(CanExecute = nameof(CanLoadCurve))]
    public async Task LoadCurveAsync()
    {
        _curveLoadCancellation?.Cancel();
        Curve = Array.Empty<CurvePoint>();  // never show the previous hull's curve under the new hull
        if (_selection.SelectedHull is not { } hull)
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        _curveLoadCancellation = cancellation;
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            IReadOnlyList<CurvePoint> points = await _api.GetCurveAsync(hull.Id, CurvePointCount, cancellation.Token);
            if (!cancellation.IsCancellationRequested)
            {
                Curve = points;
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Superseded by a newer hull selection.
        }
        catch (HydroApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            if (_curveLoadCancellation == cancellation)
            {
                _curveLoadCancellation = null;
                IsLoading = false;
            }

            cancellation.Dispose();
        }
    }

    private bool CanLoadCurve() => !IsLoading;

    // ---- Reacting to the shared selection ----

    private void OnSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SelectionService.SelectedHull):
                _ = LoadCurveAsync();  // fire and forget: LoadCurveAsync handles its own errors
                break;

            case nameof(SelectionService.SelectedRow):
                HydrostaticRow? row = _selection.SelectedRow;
                Marker = row is null ? null : new CurvePoint(row.Draft, row.Displacement);
                break;
        }
    }

    // ---- Keeping the LiveCharts objects in step with the plain state ----

    partial void OnCurveChanged(IReadOnlyList<CurvePoint> value) =>
        _curveSeries.Values = value.Select(point => new ObservablePoint(point.Draft, point.Displacement)).ToArray();

    partial void OnMarkerChanged(CurvePoint? value)
    {
        _markerSeries.Values = value is null
            ? Array.Empty<ObservablePoint>()
            : new[] { new ObservablePoint(value.Draft, value.Displacement) };

        _markerLine.Xi = value?.Draft;
        _markerLine.Xj = value?.Draft;
        _markerLine.IsVisible = value is not null;
    }

    private static Axis CreateAxis(string name, Func<double, string> labeler) => new()
    {
        Name = name,
        NamePaint = new SolidColorPaint(AxisColor),
        NameTextSize = 13,
        LabelsPaint = new SolidColorPaint(AxisColor),
        TextSize = 12,
        Labeler = labeler,
        MinLimit = 0,
        SeparatorsPaint = new SolidColorPaint(AxisColor.WithAlpha(40), 1),
    };
}
