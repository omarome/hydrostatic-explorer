---
description: Add a new view + ViewModel wired into the shared selection
argument-hint: <what the view should show>
---
Add a new view to HydroExplorer.App: $ARGUMENTS

Before writing code, read `HullViewModel.cs`, `HullView.axaml`, `MainViewModel.cs`, `MainWindow.axaml` and `App.axaml.cs`, then propose the ViewModel's properties/commands and where the view goes in the window. Wait for my OK.

Then follow the existing patterns:
1. ViewModel in `ViewModels/`, deriving from `ViewModelBase`, using `[ObservableProperty]` / `[RelayCommand]`. No Avalonia control types.
2. If it uses the selection, inject `SelectionService` and react to its `PropertyChanged`. Write back with `SelectDraft(...)` so values snap to the 1 cm grid.
3. Register it as a singleton in `App.axaml.cs`, expose it from `MainViewModel`, place the view in `MainWindow.axaml` with `DataContext="{Binding ...}"`.
4. View in `Views/` as `.axaml` with `x:DataType` (compiled bindings). Code-behind only for view glue.
5. Tests in `tests/HydroExplorer.App.Tests` using `SelectionService` and `FakeHydroApiClient`.
6. Run `dotnet build` and `dotnet test`. Then tell me exactly what to click in the running app to check it visually.
