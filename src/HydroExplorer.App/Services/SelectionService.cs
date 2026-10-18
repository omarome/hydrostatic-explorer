using CommunityToolkit.Mvvm.ComponentModel;
using HydroExplorer.Core.Models;

namespace HydroExplorer.App.Services;

/// <summary>
/// The one shared selection that keeps every view in sync: which hull, and which hydrostatic row.
/// </summary>
/// <remarks>
/// This is "lifting state up" from React: instead of the table telling the chart directly, both
/// read and write one shared owner of the state. It is registered as a singleton, so every
/// ViewModel that asks for it in its constructor gets the same instance (much like a React context
/// provider at the root). ViewModels react to changes by subscribing to PropertyChanged.
/// </remarks>
public partial class SelectionService : ObservableObject
{
    [ObservableProperty]
    private Hull? _selectedHull;

    [ObservableProperty]
    private HydrostaticRow? _selectedRow;

    // A row only makes sense for the hull it came from, so changing hull clears the row.
    // Because this runs before the SelectedHull notification is raised, subscribers never
    // see the new hull together with the old hull's row.
    partial void OnSelectedHullChanged(Hull? value) => SelectedRow = null;
}
