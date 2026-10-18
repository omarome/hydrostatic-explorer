using CommunityToolkit.Mvvm.ComponentModel;
using HydroExplorer.Core.Hydrostatics;
using HydroExplorer.Core.Models;

namespace HydroExplorer.App.Services;

/// <summary>
/// The one shared selection that keeps every view in sync: which hull, and which draft.
/// </summary>
/// <remarks>
/// This is "lifting state up" from React: instead of the table telling the chart directly, every view
/// reads and writes one shared owner of the state. It is registered as a singleton, so every ViewModel
/// that asks for it in its constructor gets the same instance (much like a React context provider at
/// the root). ViewModels react to changes by subscribing to PropertyChanged.
///
/// The selection is a draft, not a table row: the chart and slider can pick any draft instantly, and
/// the table then shows the row for that draft (fetching another page if needed).
/// </remarks>
public partial class SelectionService : ObservableObject
{
    [ObservableProperty]
    private Hull? _selectedHull;

    /// <summary>
    /// The selected draft (m), always equal to a table row's draft, or null for no selection.
    /// Set it through <see cref="SelectDraft"/> unless the value came from a table row.
    /// </summary>
    [ObservableProperty]
    private double? _selectedDraft;

    /// <summary>
    /// Selects the table draft nearest to <paramref name="draft"/> (rounded to whole cm and kept within
    /// the hull's draft range). Does nothing when no hull is selected.
    /// </summary>
    public void SelectDraft(double draft)
    {
        if (SelectedHull is { } hull)
        {
            SelectedDraft = HydrostaticTableGenerator.SnapToTableDraft(hull, draft);
        }
    }

    // A draft only makes sense for the hull it was chosen on, so changing hull clears it.
    // This runs before the SelectedHull notification is raised, so subscribers never see the
    // new hull together with the old hull's draft.
    partial void OnSelectedHullChanged(Hull? value) => SelectedDraft = null;
}
