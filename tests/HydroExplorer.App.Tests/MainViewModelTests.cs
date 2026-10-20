using HydroExplorer.App.Services;
using HydroExplorer.App.ViewModels;

namespace HydroExplorer.App.Tests;

/// <summary>The theme toggle button's behaviour, in isolation from the window.</summary>
public class MainViewModelTests
{
    private static MainViewModel CreateViewModel(FakeThemeService theme)
    {
        var api = new FakeHydroApiClient();
        var selection = new SelectionService();
        var table = new HydroTableViewModel(api, selection, new FakeFileSaveService());
        var chart = new HydroChartViewModel(api, selection);
        var hull = new HullViewModel(selection);
        var status = new StatusBarViewModel(new ApiStatus(), new HttpClient { BaseAddress = new Uri("http://localhost") });
        return new MainViewModel(table, chart, hull, status, theme);
    }

    [Fact]
    public void StartsShowingTheIconForTheOppositeOfTheCurrentTheme()
    {
        MainViewModel darkStart = CreateViewModel(new FakeThemeService(AppTheme.Dark));
        Assert.Equal(AppTheme.Dark, darkStart.CurrentTheme);
        Assert.Equal("☀", darkStart.ThemeToggleIcon);
        Assert.Equal("Switch to light mode", darkStart.ThemeToggleTooltip);

        MainViewModel lightStart = CreateViewModel(new FakeThemeService(AppTheme.Light));
        Assert.Equal(AppTheme.Light, lightStart.CurrentTheme);
        Assert.Equal("🌙", lightStart.ThemeToggleIcon);
        Assert.Equal("Switch to dark mode", lightStart.ThemeToggleTooltip);
    }

    [Fact]
    public void TogglingFromDark_SwitchesToLight_AndFlipsTheIcon()
    {
        var theme = new FakeThemeService(AppTheme.Dark);
        MainViewModel viewModel = CreateViewModel(theme);

        viewModel.ToggleThemeCommand.Execute(null);

        Assert.Equal(AppTheme.Light, viewModel.CurrentTheme);
        Assert.Equal("🌙", viewModel.ThemeToggleIcon);
        Assert.Equal(new[] { AppTheme.Light }, theme.SetThemeCalls);
    }

    [Fact]
    public void TogglingTwice_ReturnsToTheStartingTheme()
    {
        var theme = new FakeThemeService(AppTheme.Light);
        MainViewModel viewModel = CreateViewModel(theme);

        viewModel.ToggleThemeCommand.Execute(null);
        viewModel.ToggleThemeCommand.Execute(null);

        Assert.Equal(AppTheme.Light, viewModel.CurrentTheme);
        Assert.Equal(new[] { AppTheme.Dark, AppTheme.Light }, theme.SetThemeCalls);
    }
}
