namespace HydroExplorer.App.ViewModels;

/// <summary>
/// ViewModel for the main window. It composes the child ViewModels, much like a React page
/// component that renders child components and passes each one its props.
/// </summary>
public class MainViewModel : ViewModelBase
{
    public MainViewModel(HydroTableViewModel table, HydroChartViewModel chart, HullViewModel hull)
    {
        Table = table;
        Chart = chart;
        Hull = hull;
    }

    public string Title => "Hydrostatics Explorer";

    /// <summary>Hull selector and paginated table.</summary>
    public HydroTableViewModel Table { get; }

    /// <summary>Displacement curve with the selected-draft marker.</summary>
    public HydroChartViewModel Chart { get; }

    /// <summary>Hull side profile with the waterline at the selected draft.</summary>
    public HullViewModel Hull { get; }

    /// <summary>Loads the initial data. Called once when the window opens.</summary>
    public Task InitializeAsync() => Table.LoadHullsAsync();
}
