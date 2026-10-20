# HydroExplorer: guide for AI agents

Desktop demo app showing hydrostatic data for box barges and ship-shaped hulls in three linked views (paged table,
displacement curve, hull 3D/side view), fed by a local REST API with server-side paging.
Built as interview prep for a WPF role, so keep code clear, idiomatic and easy to explain.

More context: README.md (run, API reference), ARCHITECTURE.md (data flow, selection sharing),
DECISIONS.md (why each choice was made), INTERVIEW_NOTES.md (likely questions).

## Stack
- .NET 8 (SDK pinned in `global.json`), C# latest, xUnit 2.9
- **Avalonia 11.3** (not WPF, not Avalonia 12). Views are `.axaml`. Runs on macOS (Apple Silicon dev machine).
- MVVM via CommunityToolkit.Mvvm (`[ObservableProperty]`, `[RelayCommand]`), DI via Microsoft.Extensions.DependencyInjection
- DataGrid: Avalonia.Controls.DataGrid. Chart: LiveCharts2 (`LiveChartsCore.SkiaSharpView.Avalonia`)
- API: ASP.NET Core minimal API + Swashbuckle, http://localhost:5080

## Commands (run from repo root)
```bash
dotnet build                                   # warnings are errors; must be clean
dotnet test                                    # all three test projects must pass
dotnet test tests/HydroExplorer.Core.Tests     # one project
dotnet test --filter "FullyQualifiedName~HullViewModelTests"   # one class
dotnet run --project src/HydroExplorer.Api     # API on :5080 (start first)
dotnet run --project src/HydroExplorer.App     # desktop app
```
- After any change: `dotnet build`, then `dotnet test`. Don't call a task done until both pass.
- You can't see the UI. For visual changes, say exactly what to click and what to look for, and I'll check.
- Check the API with curl (examples in README.md) when you change endpoints.

## Projects
```
src/HydroExplorer.Core   Models/, Hydrostatics/, Paging/, Export/, Data/. No UI, no HTTP. Depends on nothing.
src/HydroExplorer.Api    Program.cs, Endpoints/, Queries/ (validate → filter → sort → page), Infrastructure/
src/HydroExplorer.App    Services/, ViewModels/, Views/ (.axaml), Controls/ (custom-drawn hull views), Rendering/ (3D camera, mesh, draw order, shading; pure and tested)
tests/*.Core.Tests       formulas, table generation, snapping, paging, CSV
tests/*.Api.Tests        WebApplicationFactory integration tests (ApiFactory.cs)
tests/*.App.Tests        ViewModels with FakeHydroApiClient / FakeFileSaveService, layout math
```

## Architecture rules
- `SelectionService` (singleton) owns the shared state: `SelectedHull` + `SelectedDraft`. Views and ViewModels
  write to it and react to its `PropertyChanged`. Don't add direct ViewModel-to-ViewModel links.
- The **draft** is the shared selection, not a table row. Only the table ViewModel talks to the network
  when the draft is on another page (`page-of` endpoint).
- All DI registrations are singletons in `App.axaml.cs` → `ConfigureServices()`. New services go there.
- Views are placed explicitly in XAML (`DataContext="{Binding Table}"`). There is no ViewLocator; don't add one.
- Core records (`Hull`, `HydrostaticRow`, `CurvePoint`, `PagedResult<T>`) are shared by API and App so
  JSON maps onto the same types. Changing them affects both sides and their tests.

## MVVM rules
- ViewModels: no Avalonia control types, no `Window`, no dialogs. Use injected services
  (e.g. `IFileSaveService`, `IHydroApiClient`) so ViewModels stay unit-testable.
- Code-behind only for view glue (header-click sort, key routing, chart pixel → draft, scroll into view).
- Use `[ObservableProperty]` + `[NotifyPropertyChangedFor]`, `partial void OnXChanged`, `[RelayCommand]` with `CanExecute`.
- Async loads: set `IsLoading`, cancel the previous request, ignore stale responses, on failure keep the
  previous data and set `ErrorMessage`. Follow the existing pattern in `HydroTableViewModel`.
- Every new ViewModel behaviour gets a test in `tests/HydroExplorer.App.Tests` using the fakes.

## Styling: one source of truth
- **All colours live in `src/HydroExplorer.App/Styles/Palette.axaml`** (a ResourceDictionary with Light/Dark
  `ThemeDictionaries`). No hex values, `Colors.X`, `Color.Parse`, `SKColor.Parse` or named colours anywhere else.
- Named by role, not by hue: `AccentBrush`, `WaterBrush`, `MarkerBrush`, `SuccessBrush`, `ErrorBrush`,
  `MutedTextBrush`, `PanelBorderBrush`… Add a new role to the palette before using it.
- Spacing, corner radii and font sizes are resources too (`PanelPadding`, `PanelCornerRadius`, `SmallFontSize`).
- Control styles live in `Styles/AppStyles.axaml`, included from `App.axaml`. Views use classes, not inline setters.
- Reference palette entries with `{DynamicResource ...}` so light/dark switching works.
- C# drawing code (custom controls, LiveCharts paints) gets colours from the palette: custom controls expose
  `StyledProperty<IBrush>` set by a style; the chart resolves palette resources in the view layer.
  ViewModels never hold colours.
- Run `/style-audit` to find and fix violations.

## XAML (.axaml) rules
- Compiled bindings are on (`AvaloniaUseCompiledBindingsByDefault`): every view needs `x:DataType`.
  A wrong binding path is a **build error**, so build after every XAML change.
- Styling uses Avalonia style selectors and classes (`Border.panel`, `Classes.connected`), not WPF keyed styles.
- Custom controls use `StyledProperty` + `AffectsRender` and override `Render(DrawingContext)`.
  Keep layout math in the separate `*Layout` classes so it stays testable.

## Numbers and culture (important)
- Table drafts come from integer centimetres (`cm / 100.0`). Never build drafts by adding 0.01 in a loop.
- Snap chart/slider drafts with `SnapToTableDraft` so they compare equal (`==`) to row drafts.
- Keep domain/API values unrounded; format only in the UI.
- Use `CultureInfo.InvariantCulture` for URLs, CSV and any parsing/formatting that isn't shown to the user.
- **Box = exact, ship = integrated.** `Hull.Shape == null` → exact box formulas in `HydrostaticsCalculator`;
  otherwise `NumericalHydrostatics` (Simpson's rule over `HullGeometry.HalfBreadth`). Keep `Calculate`'s
  signature; the integrator must keep reproducing the box formulas (`NumericalHydrostaticsTests`).

## API rules
- Invalid query parameters → 400 ProblemDetails naming the parameter; unknown hull → 404.
- Every `/api` call has a 200 ms simulated delay (`SimulatedLatency:Milliseconds` in the API's appsettings.json).
  Tests rely on it being configurable; don't remove it.
- If you add or change an endpoint, update the API reference table in README.md.

## Code style
- File-scoped namespaces, nullable enabled (no `!` without a comment saying why).
- Comment the *why*, briefly. Existing comments often explain WPF/React equivalents; keep that style.
- Async methods end in `Async` and take a `CancellationToken` where reasonable. No `.Result` / `.Wait()`.

## Do not
- Don't upgrade to Avalonia 12, .NET 9/10, or 9.x/10.x Microsoft.Extensions packages (see DECISIONS.md).
- Don't add NuGet packages without asking.
- Don't edit `bin/`, `obj/`, or generated files.
- Don't commit unless I ask. When you make a notable design choice, add it to DECISIONS.md under the right phase.
