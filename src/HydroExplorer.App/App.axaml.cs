using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HydroExplorer.App.ViewModels;
using HydroExplorer.App.Views;

namespace HydroExplorer.App;

public partial class App : Application
{
    public override void Initialize()
    {
        // Loads App.axaml (styles, resources). "partial class": the other half is generated from the XAML.
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // DataContext is the object the window's {Binding}s read from — the ViewModel.
            // (Phase 3 replaces this with dependency injection.)
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
