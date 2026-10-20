using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HydroExplorer.App.Services;
using HydroExplorer.App.ViewModels;
using HydroExplorer.App.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
            ServiceProvider services = ConfigureServices();
            var mainViewModel = services.GetRequiredService<MainViewModel>();

            // DataContext is the object the window's {Binding}s read from: the ViewModel.
            desktop.MainWindow = new MainWindow { DataContext = mainViewModel };
            desktop.MainWindow.Opened += async (_, _) => await mainViewModel.InitializeAsync();
            desktop.Exit += (_, _) => services.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Dependency injection setup: registers how each service is created. The container then
    /// builds MainViewModel and passes each constructor what it asks for (constructor injection).
    /// Comparable to providing values through React context, but resolved by type.
    /// </summary>
    private static ServiceProvider ConfigureServices()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        string baseUrl = configuration["Api:BaseUrl"]
            ?? throw new InvalidOperationException("Api:BaseUrl is missing from appsettings.json.");

        var services = new ServiceCollection();

        // Singleton = one instance for the whole app. One HttpClient should be reused, not created per request.
        services.AddSingleton(new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(10),
        });
        services.AddSingleton<ApiStatus>();         // the client reports into it; the status bar reads it
        services.AddSingleton<IHydroApiClient, HydroApiClient>();
        services.AddSingleton<SelectionService>();  // the one shared selection every ViewModel receives
        services.AddSingleton<IFileSaveService, FileSaveService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<HydroTableViewModel>();
        services.AddSingleton<HydroChartViewModel>();
        services.AddSingleton<HullViewModel>();
        services.AddSingleton<StatusBarViewModel>();
        services.AddSingleton<MainViewModel>();

        return services.BuildServiceProvider();
    }
}
