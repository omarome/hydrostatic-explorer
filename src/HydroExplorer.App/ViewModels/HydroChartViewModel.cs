using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HydroExplorer.App.Services;
using HydroExplorer.Core.Hydrostatics;
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
/// from the API, and a new draft only moves the marker (no request). Clicking the chart selects a draft.
/// </summary>
/// <remarks>
/// The plain data (<see cref="Curve"/>, <see cref="Marker"/>) is what tests check. The LiveCharts
/// objects (<see cref="Series"/>, axes, sections) are created once and updated from that data.
/// </remarks>
public partial class HydroChartViewModel : ViewModelBase
{
    /// <summary>Number of curve points requested from the API. Plenty for a smooth line.</summary>
    public const int CurvePointCount = 100;

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

        // Colours are not set here: ViewModels never hold colours (see CLAUDE.md). HydroChartView
        // resolves the palette's brushes and calls ApplyColors below, once at startup and again on
        // every light/dark theme change.
        _curveSeries = new LineSeries<ObservablePoint>
        {
            Name = "Displacement",
            Values = Array.Empty<ObservablePoint>(),
            Fill = null,             // no shaded area under the line
            GeometrySize = 0,        // no dot on every point
            LineSmoothness = 0,      // straight segments between points
        };

        _markerSeries = new ScatterSeries<ObservablePoint>
        {
            Name = "Selected draft",
            Values = Array.Empty<ObservablePoint>(),
            GeometrySize = 14,
        };

        // A section with Xi == Xj draws a vertical line: here a dashed line at the selected draft.
        _markerLine = new RectangularSection
        {
            IsVisible = false,
        };

        Series = new ISeries[] { _curveSeries, _markerSeries };
        Sections = new[] { _markerLine };
        XAxes = new[] { CreateAxis("Draft T (m)", value => value.ToString("0.##")) };
        YAxes = new[] { CreateAxis("Displacement Δ (t)", value => value.ToString("N0")) };

        // Subscribe to the shared selection. Both objects are app-lifetime singletons, so this
        // subscription never needs removing (see DECISIONS.md, Phase 4).
        _selection.PropertyChanged += OnSelectionChanged;
    }

    /// <summary>
    /// Paints every LiveCharts object from colours the view resolved out of Palette.axaml. Called by
    /// HydroChartView once its DataContext is set, and again whenever the active theme changes.
    /// </summary>
    public void ApplyColors(SKColor curve, SKColor marker, SKColor markerOutline, SKColor axisColor)
    {
        _curveSeries.Stroke = new SolidColorPaint(curve, 2);
        _markerSeries.Fill = new SolidColorPaint(marker);
        _markerSeries.Stroke = new SolidColorPaint(markerOutline, 2);
        _markerLine.Stroke = new SolidColorPaint(marker, 1) { PathEffect = new DashEffect(new float[] { 4, 4 }) };

        foreach (Axis axis in XAxes.Concat(YAxes))
        {
            axis.NamePaint = new SolidColorPaint(axisColor);
            axis.LabelsPaint = new SolidColorPaint(axisColor);
            axis.SeparatorsPaint = new SolidColorPaint(axisColor.WithAlpha(40), 1);
        }
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
        ? "Click the chart or a row to select a draft"
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

            case nameof(SelectionService.SelectedDraft):
                UpdateMarker();
                break;
        }
    }

    /// <summary>
    /// Called when the user clicks or drags on the chart at <paramref name="draft"/> (chart x-coordinate).
    /// The shared selection snaps it to the nearest table draft; the table then shows that row.
    /// </summary>
    public void SelectDraftAt(double draft) => _selection.SelectDraft(draft);

    /// <summary>
    /// Places the marker at the selected draft. Displacement is read off the loaded curve, so the marker
    /// moves instantly without waiting for the table to fetch the row.
    /// </summary>
    private void UpdateMarker()
    {
        double? draft = _selection.SelectedDraft;
        double? displacement = draft is null ? null : CurveInterpolator.InterpolateDisplacement(Curve, draft.Value);
        Marker = draft is null || displacement is null ? null : new CurvePoint(draft.Value, displacement.Value);
    }

    // ---- Keeping the LiveCharts objects in step with the plain state ----

    partial void OnCurveChanged(IReadOnlyList<CurvePoint> value)
    {
        _curveSeries.Values = value.Select(point => new ObservablePoint(point.Draft, point.Displacement)).ToArray();
        UpdateMarker();  // the marker's displacement is read off the curve
    }

    partial void OnMarkerChanged(CurvePoint? value)
    {
        _markerSeries.Values = value is null
            ? Array.Empty<ObservablePoint>()
            : new[] { new ObservablePoint(value.Draft, value.Displacement) };

        _markerLine.Xi = value?.Draft;
        _markerLine.Xj = value?.Draft;
        _markerLine.IsVisible = value is not null;
    }

    // NamePaint/LabelsPaint/SeparatorsPaint are left unset here; ApplyColors fills them in once the
    // view has resolved the palette's AxisBrush.
    private static Axis CreateAxis(string name, Func<double, string> labeler) => new()
    {
        Name = name,
        NameTextSize = 13,
        TextSize = 12,
        Labeler = labeler,
        MinLimit = 0,
    };
}
