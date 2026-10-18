namespace HydroExplorer.App.ViewModels;

/// <summary>
/// ViewModel for the main window. It composes the child ViewModels, much like a React page
/// component that renders child components and passes each one its props.
/// </summary>
public class MainViewModel : ViewModelBase
{
    public MainViewModel(HydroTableViewModel table, HydroChartViewModel chart, HullViewModel hull, StatusBarViewModel status)
    {
        Table = table;
        Chart = chart;
        Hull = hull;
        Status = status;
    }

    public string Title => "Hydrostatics Explorer";

    /// <summary>Hull selector and paginated table.</summary>
    public HydroTableViewModel Table { get; }

    /// <summary>Displacement curve with the selected-draft marker.</summary>
    public HydroChartViewModel Chart { get; }

    /// <summary>Hull side profile with the waterline at the selected draft.</summary>
    public HullViewModel Hull { get; }

    /// <summary>API connection state and last load time.</summary>
    public StatusBarViewModel Status { get; }

    /// <summary>Loads the initial data. Called once when the window opens.</summary>
    public Task InitializeAsync() => Table.LoadHullsAsync();
}
