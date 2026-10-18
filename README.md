# Hydrostatics Explorer

A small desktop demo app: hydrostatic data for a box-shaped hull, shown in three connected views
(table, curve, hull side view). The data comes from a local REST API with server-side paging.

- **HydroExplorer.Core**: domain model and hydrostatics math (no UI, no HTTP)
- **HydroExplorer.Api**: ASP.NET Core minimal API serving hulls and hydrostatic tables
- **HydroExplorer.App**: Avalonia 11 MVVM desktop app (runs on macOS, Windows, Linux)

See `DECISIONS.md` for design choices and why each was made.

## Prerequisites

- .NET 8 SDK (`dotnet --list-sdks` should show `8.0.x`). `global.json` pins the solution to SDK 8.0.x.
- macOS, Windows or Linux. Developed on macOS (Apple Silicon).
- Optional: VS Code with the C# Dev Kit extension.

## Build and test

Run these from the repository root:

```bash
dotnet build
dotnet test
```

## Run

The API and the app are separate processes. Start the API first, in its own terminal:

```bash
dotnet run --project src/HydroExplorer.Api
```

The API listens on **http://localhost:5080** (set in `src/HydroExplorer.Api/Properties/launchSettings.json`).
To check it is up:

```bash
curl http://localhost:5080/
```

Then start the desktop app in a second terminal:

```bash
dotnet run --project src/HydroExplorer.App
```

## Project layout

```
HydroExplorer.sln
Directory.Build.props        # settings shared by all projects (net8.0, nullable, warnings as errors)
global.json                  # pins the .NET SDK version
src/
  HydroExplorer.Core/
  HydroExplorer.Api/
  HydroExplorer.App/
tests/
  HydroExplorer.Core.Tests/  # xUnit: formulas, row generation, paging
  HydroExplorer.Api.Tests/   # xUnit + WebApplicationFactory: endpoints
  HydroExplorer.App.Tests/   # xUnit: ViewModels with a fake API client
```
