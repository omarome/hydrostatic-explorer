using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using HydroExplorer.App.ViewModels;
using LiveChartsCore.Drawing;
using SkiaSharp;

namespace HydroExplorer.App.Views;

/// <summary>
/// Code-behind for the chart: turns mouse clicks and drags into draft selections, and keeps the
/// LiveCharts paints in step with the palette. Converting a pixel position to chart coordinates
/// needs the chart control itself, so it belongs here; the ViewModel only receives the resulting
/// draft. Colours are resolved here too (see CLAUDE.md: "ViewModels never hold colours") and pushed
/// into the ViewModel as plain SkiaSharp colours via ApplyColors.
/// </summary>
public partial class HydroChartView : UserControl
{
    public HydroChartView()
    {
        InitializeComponent();

        // Re-resolve colours on every light/dark switch (ActualThemeVariantChanged is Avalonia's
        // theme-variant equivalent of a WPF SystemEvents.UserPreferenceChanged handler).
        ActualThemeVariantChanged += (_, _) => ApplyPaletteColors();
    }

    private HydroChartViewModel? ViewModel => DataContext as HydroChartViewModel;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        ApplyPaletteColors();
    }

    private void ApplyPaletteColors()
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        viewModel.ApplyColors(
            ResolveColor("CurveBrush"), ResolveColor("MarkerBrush"), ResolveColor("MarkerOutlineBrush"), ResolveColor("AxisBrush"));
    }

    private SKColor ResolveColor(string resourceKey)
    {
        // Application.Current, not `this`: CurveBrush/MarkerBrush/... live inside Palette.axaml's
        // ResourceDictionary.ThemeDictionaries, and a control's own TryFindResource/TryGetResource
        // does not walk up far enough to find those — only Application.Current.TryGetResource does.
        // (DynamicResource in XAML uses a different resolution path and isn't affected.)
        Color color = Application.Current is { } app && app.TryGetResource(resourceKey, ActualThemeVariant, out object? value)
            && value is ISolidColorBrush brush
            ? brush.Color
            : default;
        return new SKColor(color.R, color.G, color.B, color.A);
    }

    private void OnChartPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(Chart).Properties.IsLeftButtonPressed)
        {
            SelectDraftAtPointer(e);
        }
    }

    /// <summary>Dragging with the button held scrubs through drafts.</summary>
    private void OnChartPointerMoved(object? sender, PointerEventArgs e)
    {
        if (e.GetCurrentPoint(Chart).Properties.IsLeftButtonPressed)
        {
            SelectDraftAtPointer(e);
        }
    }

    private void SelectDraftAtPointer(PointerEventArgs e)
    {
        Avalonia.Point position = e.GetPosition(Chart);

        // ScalePixelsToData converts a pixel position inside the chart to (x, y) in data units;
        // x is the draft in metres.
        LvcPointD data = Chart.ScalePixelsToData(new LvcPointD(position.X, position.Y));
        ViewModel?.SelectDraftAt(data.X);
    }
}
