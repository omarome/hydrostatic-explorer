using Avalonia;
using Avalonia.Styling;

namespace HydroExplorer.App.Services;

/// <summary>
/// <see cref="IThemeService"/> backed by Avalonia's theme variant. Starts from whatever the OS is
/// currently resolving to (<see cref="Application.ActualThemeVariant"/>), then <see cref="SetTheme"/>
/// pins <see cref="Application.RequestedThemeVariant"/> explicitly, which stops the app following the
/// OS setting from then on.
/// </summary>
/// <remarks>
/// WPF note: WPF has no built-in light/dark theme variant; the usual equivalent is swapping a
/// Light/Dark ResourceDictionary into Application.Resources.MergedDictionaries at runtime.
/// </remarks>
public sealed class ThemeService : IThemeService
{
    public AppTheme Current { get; private set; } = Application.Current?.ActualThemeVariant == ThemeVariant.Dark
        ? AppTheme.Dark
        : AppTheme.Light;

    public void SetTheme(AppTheme theme)
    {
        Current = theme;
        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = theme == AppTheme.Dark ? ThemeVariant.Dark : ThemeVariant.Light;
        }
    }
}
