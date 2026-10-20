namespace HydroExplorer.App.Services;

/// <summary>The two theme variants the app's toggle button switches between.</summary>
public enum AppTheme
{
    Light,
    Dark,
}

/// <summary>
/// Reads and sets the app's active theme. An interface so the ViewModel that owns the toggle button
/// doesn't reference Avalonia's theming types directly, and tests can use a fake.
/// </summary>
public interface IThemeService
{
    /// <summary>The theme in effect right now (the OS setting, until <see cref="SetTheme"/> overrides it).</summary>
    AppTheme Current { get; }

    /// <summary>Switches the app to <paramref name="theme"/>, overriding the OS setting from now on.</summary>
    void SetTheme(AppTheme theme);
}
