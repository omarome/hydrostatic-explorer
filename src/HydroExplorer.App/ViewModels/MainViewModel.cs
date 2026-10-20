using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HydroExplorer.App.Services;

namespace HydroExplorer.App.ViewModels;

/// <summary>
/// ViewModel for the main window. It composes the child ViewModels, much like a React page
/// component that renders child components and passes each one its props.
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly IThemeService _themeService;

    public MainViewModel(
        HydroTableViewModel table, HydroChartViewModel chart, HullViewModel hull, StatusBarViewModel status,
        IThemeService themeService)
    {
        Table = table;
        Chart = chart;
        Hull = hull;
        Status = status;
        _themeService = themeService;
        _currentTheme = themeService.Current;
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

    // ---- Theme toggle ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThemeToggleIcon))]
    [NotifyPropertyChangedFor(nameof(ThemeToggleTooltip))]
    private AppTheme _currentTheme;

    /// <summary>
    /// The glyph shows the theme a click switches *to*: a sun while dark (click for daylight), a moon
    /// while light (click for night). The paging buttons elsewhere use the same plain-Unicode-glyph
    /// approach, so this needs no new icon asset or style.
    /// </summary>
    public string ThemeToggleIcon => CurrentTheme == AppTheme.Dark ? "☀" : "🌙";

    public string ThemeToggleTooltip => CurrentTheme == AppTheme.Dark ? "Switch to light mode" : "Switch to dark mode";

    [RelayCommand]
    private void ToggleTheme()
    {
        CurrentTheme = CurrentTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;
        _themeService.SetTheme(CurrentTheme);
    }
}
