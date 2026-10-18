using Avalonia.Controls;
using Avalonia.Input;
using HydroExplorer.App.ViewModels;
using LiveChartsCore.Drawing;

namespace HydroExplorer.App.Views;

/// <summary>
/// Code-behind for the chart: turns mouse clicks and drags into draft selections. Converting a pixel
/// position to chart coordinates needs the chart control itself, so it belongs in the view; the
/// ViewModel only receives the resulting draft.
/// </summary>
public partial class HydroChartView : UserControl
{
    public HydroChartView()
    {
        InitializeComponent();
    }

    private HydroChartViewModel? ViewModel => DataContext as HydroChartViewModel;

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
