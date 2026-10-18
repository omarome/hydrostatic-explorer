namespace HydroExplorer.App.ViewModels;

/// <summary>
/// ViewModel for the main window. It composes the child ViewModels, much like a React page
/// component that renders child components and passes each one its props.
/// </summary>
public class MainViewModel : ViewModelBase
{
    public MainViewModel(HydroTableViewModel table)
    {
        Table = table;
    }

    public string Title => "Hydrostatics Explorer";

    /// <summary>Hull selector and paginated table.</summary>
    public HydroTableViewModel Table { get; }

    /// <summary>Loads the initial data. Called once when the window opens.</summary>
    public Task InitializeAsync() => Table.LoadHullsAsync();
}
