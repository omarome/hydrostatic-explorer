using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using HydroExplorer.App.Services;
using HydroExplorer.Core.Models;

namespace HydroExplorer.App.ViewModels;

/// <summary>
/// Feeds the hull side view: the selected hull's dimensions and the selected row's draft.
/// No API calls. Everything comes from the shared selection.
/// </summary>
public partial class HullViewModel : ViewModelBase
{
    private readonly SelectionService _selection;

    public HullViewModel(SelectionService selection)
    {
        _selection = selection;
        _selection.PropertyChanged += OnSelectionChanged;  // app-lifetime singletons: no unsubscribe needed
        UpdateFromSelection();
    }

    [ObservableProperty]
    private string _hullName = "No hull selected";

    [ObservableProperty]
    private double _length;

    [ObservableProperty]
    private double _depth;

    /// <summary>Null when no row is selected (no waterline drawn).</summary>
    [ObservableProperty]
    private double? _draft;

    private void OnSelectionChanged(object? sender, PropertyChangedEventArgs e) => UpdateFromSelection();

    private void UpdateFromSelection()
    {
        Hull? hull = _selection.SelectedHull;
        HullName = hull?.Name ?? "No hull selected";
        Length = hull?.Length ?? 0;
        Depth = hull?.Depth ?? 0;
        Draft = _selection.SelectedRow?.Draft;
    }
}
