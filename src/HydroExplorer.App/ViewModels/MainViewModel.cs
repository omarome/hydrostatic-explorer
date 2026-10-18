using CommunityToolkit.Mvvm.ComponentModel;

namespace HydroExplorer.App.ViewModels;

/// <summary>ViewModel for the main window. Phase 0: just a title to prove binding works.</summary>
public partial class MainViewModel : ViewModelBase
{
    // [ObservableProperty] generates a public "Title" property that raises PropertyChanged,
    // so the UI updates when it changes (roughly like a useState value).
    [ObservableProperty]
    private string _title = "Hydrostatics Explorer";
}
