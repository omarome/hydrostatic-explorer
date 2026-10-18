using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using HydroExplorer.App.Services;
using HydroExplorer.Core.Hydrostatics;
using HydroExplorer.Core.Models;

namespace HydroExplorer.App.ViewModels;

/// <summary>
/// Feeds the hull side view and its draft slider. No API calls: the hull's dimensions and the draft
/// come from the shared selection, and moving the slider writes the draft back to it.
/// </summary>
public partial class HullViewModel : ViewModelBase
{
    private readonly SelectionService _selection;

    // True while we copy values *from* the selection, so the slider's change handler doesn't
    // write them straight back as if the user had moved the slider.
    private bool _isUpdatingFromSelection;

    public HullViewModel(SelectionService selection)
    {
        _selection = selection;
        _selection.PropertyChanged += OnSelectionChanged;  // app-lifetime singletons: no unsubscribe needed
        UpdateFromSelection();
    }

    [ObservableProperty]
    private string _hullName = "No hull selected";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHull))]
    private double _length;

    [ObservableProperty]
    private double _depth;

    /// <summary>Null when no draft is selected (no waterline drawn).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DraftLabel))]
    private double? _draft;

    /// <summary>The slider position (m). Bound two-way: the user dragging the slider sets it.</summary>
    [ObservableProperty]
    private double _sliderDraft = HydrostaticTableGenerator.MinDraft;

    /// <summary>Slider lower limit: the first table draft.</summary>
    public double MinDraft => HydrostaticTableGenerator.MinDraft;

    public bool HasHull => Length > 0;

    /// <summary>For example "T = 3.25 m", or "T = –" when nothing is selected.</summary>
    public string DraftLabel => Draft is double draft ? $"T = {draft:F2} m" : "T = –";

    /// <summary>The user moved the slider: select that draft (snapped to whole centimetres).</summary>
    partial void OnSliderDraftChanged(double value)
    {
        if (!_isUpdatingFromSelection)
        {
            _selection.SelectDraft(value);
        }
    }

    private void OnSelectionChanged(object? sender, PropertyChangedEventArgs e) => UpdateFromSelection();

    private void UpdateFromSelection()
    {
        _isUpdatingFromSelection = true;
        try
        {
            Hull? hull = _selection.SelectedHull;
            HullName = hull?.Name ?? "No hull selected";
            Length = hull?.Length ?? 0;
            Depth = hull?.Depth ?? 0;
            Draft = _selection.SelectedDraft;
            SliderDraft = Draft ?? MinDraft;
        }
        finally
        {
            _isUpdatingFromSelection = false;
        }
    }
}
