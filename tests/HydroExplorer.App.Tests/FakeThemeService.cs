using HydroExplorer.App.Services;

namespace HydroExplorer.App.Tests;

/// <summary>Stand-in for <see cref="ThemeService"/>: starts at a chosen theme and records every SetTheme call.</summary>
public sealed class FakeThemeService : IThemeService
{
    public FakeThemeService(AppTheme initial = AppTheme.Light)
    {
        Current = initial;
    }

    public AppTheme Current { get; private set; }

    public List<AppTheme> SetThemeCalls { get; } = new();

    public void SetTheme(AppTheme theme)
    {
        SetThemeCalls.Add(theme);
        Current = theme;
    }
}
