# Decisions

Choices made where the brief left room, with the reason for each. Newest at the bottom of each phase.

## Phase 0: setup

**Avalonia 11.3.22, not 12.** Avalonia 12 is the latest release, but the brief asks for Avalonia 11.
11.3.22 is the newest 11.x release. `Avalonia.Controls.DataGrid` 11.3.13 is the newest 11.x DataGrid.

**LiveCharts2 2.0.5 works with Avalonia 11.** `LiveChartsCore.SkiaSharpView.Avalonia` 2.0.5 requires
Avalonia ≥ 11.0.0. The usual conflict between these libraries is SkiaSharp (the 2D graphics engine both
use). Both need SkiaSharp 2.88.9, so they agree. No custom chart control is needed.

**Swagger via Swashbuckle 6.9.0.** On .NET 8, Swashbuckle is the standard way to get a Swagger UI. (.NET 9
added built-in OpenAPI document generation, but this project targets .NET 8.)

**Package versions stay on the .NET 8 line.** `Microsoft.Extensions.DependencyInjection` 8.0.1 and
`Microsoft.AspNetCore.Mvc.Testing` 8.0.32 match the .NET 8 runtime. Version 10 also installs on .NET 8,
but staying on 8.x avoids mixing framework generations.

**Test stack.** xUnit 2.9.3, xunit.runner.visualstudio 2.8.2, Microsoft.NET.Test.Sdk 17.14.1.
The template's `coverlet.collector` (code coverage) was removed because the project doesn't use it.

**Shared settings in `Directory.Build.props`.** `net8.0`, nullable reference types, implicit usings and
`TreatWarningsAsErrors` are set once for all six projects, so warnings can't pile up unnoticed.

**Compiled bindings by default.** `AvaloniaUseCompiledBindingsByDefault` is on in the app project, so every
`{Binding}` is checked at build time against the view's `x:DataType`.

**Template ViewLocator removed.** The Avalonia MVVM template includes a `ViewLocator` that finds views by
renaming ViewModel types with reflection ("FooViewModel" → "FooView"). This app has only a few views,
so it places them explicitly in XAML. That is easier to follow and avoids hidden convention-based magic.

**Fixed API port 5080, HTTP only.** The launch profile has no HTTPS, so there's no local dev certificate
to set up on a fresh machine. That's fine for a localhost demo.

**.NET SDK install location.** The SDK was installed per user into `~/.dotnet` with Microsoft's
`dotnet-install.sh`, which needs no admin rights. `~/.zshrc` adds it to `PATH` and sets `DOTNET_ROOT`.
A system-wide install from the official .pkg works the same way.

## Phase 1: core domain

**Static calculator and generator.** `HydrostaticsCalculator` and `HydrostaticTableGenerator` are pure functions
with no state or dependencies, so they are `static` classes. There is nothing to swap out in tests, so an interface
would add ceremony without a benefit.

**Rows hold unrounded values.** Only drafts are rounded (to 2 decimals). Displacement, KM and the other values
keep full precision, and the UI formats them for display. Rounding in the domain would lose precision in later
calculations, such as interpolation for the curve.

**Drafts come from integer centimetres.** The generator loops over whole centimetres (10, 11, … D·100) and computes
`cm / 100.0` for each row. Adding 0.01 repeatedly would drift: row 315 would hold 3.2499999999999747, not 3.25. A
test checks every draft for exact equality.

**Validation throws exceptions.** A non-positive, NaN or infinite hull dimension throws `ArgumentException`. A draft
outside (0, D] throws `ArgumentOutOfRangeException`. The API (Phase 2) validates its query parameters first and
returns 400, so these exceptions mean a programming error, not bad user input.

**A page past the end returns no items, not an error.** `Pager.Paginate` rejects `page < 1` or `pageSize < 1`. Asking
for page 49 of 48 returns empty `Items` with the correct `TotalCount` and `TotalPages`, so a client can recover by
jumping to the last page. An empty source has `TotalPages = 0`.

**`Pager.PageOfIndex` added now.** It is a one-line helper that Phase 6's "page containing draft X" endpoint will need.
It sits next to the other paging math so it is tested with it.

**Repository interface.** `IHullRepository` / `SeedHullRepository` keeps the API independent of where hulls come from.
The seed data is a static in-memory list.

## Phase 2: REST API

**Endpoints in their own file.** `Endpoints/HullEndpoints.cs` maps the routes with `MapGroup("/api/hulls")`.
Program.cs only wires services and middleware. The handlers are named static methods, not inline lambdas, so each
one is easy to read and to point at.

**Query rules in one place.** `Queries/HydrostaticsQuery.cs` holds the parameter limits, `Validate()` and `Apply()`
(filter → sort → page). Filtering and sorting happen on the whole table *before* paging. That order is the point of
server-side paging: page 2 sorted by KM is the 2nd page of the whole sorted table, not page 2 re-sorted.

**Validation returns every error at once.** `Validate()` collects all problems into a dictionary.
`Results.ValidationProblem` turns it into a 400 ProblemDetails body that names each bad parameter. A separate
validation library (FluentValidation) would be overkill for six parameters.

**404 is checked before 400.** An unknown hull id returns 404 even if the query parameters are also invalid. The
resource has to exist before its options matter.

**Sorting is case-insensitive with a tie-breaker.** `sortBy=KM` and `sortDir=DESC` are accepted. Rows with equal
sort keys are ordered by draft, so pages stay stable. (For a box hull, sorting by displacement gives the same order
as sorting by draft, because Δ grows linearly with T.)

**Unparsable numbers return 400, not 500.** In Development, minimal APIs throw an exception when a query value
can't be parsed (for example `page=abc`), and the exception handler turned that into a 500. Setting
`RouteHandlerOptions.ThrowOnBadRequest = false` makes it a plain 400, which `UseStatusCodePages` turns into
ProblemDetails.

**The table is generated per request, not cached.** About 1,200 rows of arithmetic takes microseconds. A cache would
add code and invalidation questions for no measurable gain.

**The curve is sampled, not taken from the table.** `/curve?points=N` computes N evenly spaced drafts from 0.10 m to
D with the calculator. The chart gets the full curve in one small response, without paging through the table.
Allowed `points`: 2–1000.

**Simulated latency is middleware plus options.** `SimulatedLatencyMiddleware` delays only `/api` paths, so
Swagger stays fast. The delay comes from `IOptionsMonitor<SimulatedLatencyOptions>`, so edits to appsettings.json
apply without a restart, and tests can override it (0 ms normally, 300 ms in the one test that checks the delay).

**JSON numbers are unrounded.** Responses carry raw doubles such as `368.99999999999994` (1.025 × 360 in binary
floating point). They are correct to 15 significant figures, and the app formats them for display.

**Integration tests use `WebApplicationFactory<Program>`.** It runs the real Program.cs in memory, so routing,
binding, validation and JSON serialization are all tested together. `public partial class Program` at the bottom of
Program.cs makes the generated `Program` class visible to the test project.

## Phase 3: app shell and paginated table

**One API client interface, one exception type.** ViewModels depend on `IHydroApiClient`. `HydroApiClient`
turns every failure into a `HydroApiException` with a message the user can read: connection refused, timeout,
non-2xx status, bad JSON. ViewModels then catch one exception type and show its message in the error banner.
The test project has a `FakeHydroApiClient` that serves real data from Core.

**Plain `ServiceCollection` for dependency injection, no Generic Host.** `App.axaml.cs` registers a single
`HttpClient` (10 s timeout), the API client and the ViewModels as singletons. That's enough for a desktop app with
one window. `IHttpClientFactory` and the full Generic Host would add concepts without solving a problem here.
The base URL comes from the app's own `appsettings.json` via `Microsoft.Extensions.Configuration.Json` 8.0.1.

**The table ViewModel owns the hull list**, as the brief says. The hull selector in MainWindow binds to
`Table.Hulls` and `Table.SelectedHull`. Phase 4 adds the shared selection that the chart and hull view follow.

**Page state changes only on success.** `LoadPageAsync(page)` sets `Rows`, `CurrentPage` and the totals only
after the request succeeds. A failed "next page" therefore leaves the UI consistent: page 1 is still shown and
labelled page 1, with the error banner above it.

**A newer request wins.** Each page load cancels the previous one through a `CancellationTokenSource`, ignores
its own result if it was superseded, and only the latest request clears `IsLoading`. Without this, switching
hulls quickly could show the first hull's page under the second hull's name. A test answers requests out of
order to prove it.

**Paging buttons are disabled while loading.** CanExecute includes `!IsLoading`. This prevents double clicks
from queueing page loads and makes the loading state visible on the buttons too.

**Retry and Refresh are one command.** `RefreshCommand` reloads the hull list if it never loaded, otherwise
the current page. The error banner's Retry button binds to the same command.

**Server-side sorting through the DataGrid's `Sorting` event.** Left alone, the DataGrid would sort only the
rows of the current page. The code-behind marks the event handled and calls `SortByCommand` with the column's
`SortMemberPath` ("draft", "displacement" or "km", the API's names). It also draws ▲/▼ in the header, because
DataGrid columns aren't in the visual tree and their headers are awkward to bind. Only the three columns the
API can sort by are sortable. Sorting returns to page 1.

**Lists are replaced, not mutated.** `Rows` and `Hulls` are `IReadOnlyList` properties that get a new list on
each load (like `setRows(newRows)` in React), not `ObservableCollection`s edited item by item.

**Number formats.** Draft and TPC have 2 decimals; KB, BM and KM have 3 (stability values are read to the mm);
volume, displacement and BML have thousands separators. Numeric cells are right-aligned via a `number` style
class on the cells. Formatting follows the OS locale.

**Compiled bindings in DataGrid columns.** Each column sets `x:DataType="models:HydrostaticRow"`, because a
column's bindings run against a row, not against the view's ViewModel.

## Phase 4: chart and shared selection

**Shared selection: a `SelectionService`, not `WeakReferenceMessenger`.** `SelectionService` is an
`ObservableObject` with two properties, `SelectedHull` and `SelectedRow`. It is registered as a DI singleton and
injected into every ViewModel that needs the selection. Views write to it, and they react to its `PropertyChanged`
event.

Why a service rather than CommunityToolkit's messenger:

- **It holds state, not just events.** Any ViewModel can read the current selection at any time. With a messenger,
  a ViewModel created after a "selection changed" message missed it, and someone has to remember the last value.
- **One place for invariants.** Changing the hull clears the row, inside the service, before anyone is notified.
  With messages, every receiver would have to apply that rule itself.
- **Explicit dependencies.** A ViewModel's constructor shows that it takes part in the selection. With a static
  messenger, the senders and receivers of a message type can only be found by searching.
- **Easy to test.** Tests create a `new SelectionService()` and pass it to the ViewModels. There is no global
  messenger to reset between tests.

The messenger's advantage is fully decoupled, many-to-many broadcasting with weak references, so a short-lived
ViewModel that forgets to unsubscribe doesn't leak. Here every ViewModel is an app-lifetime singleton, so a
plain `+=` subscription is safe and never needs removing. If ViewModels were created and discarded (tabs,
dialogs), they would need to unsubscribe, or the messenger would be the better fit.

**React comparison.** This is "lifting state up". In React, the shared selection would live in the closest common
parent (or a context provider), and the table, chart and hull view would receive `selection` and `onSelect` as
props. Here the common owner is the `SelectionService`, DI plays the role of the context provider, and
`PropertyChanged` is the re-render trigger. The difference: in React the data flows down and re-rendering does the
rest. In MVVM each ViewModel subscribes and decides what to do itself (for example, the chart reloads on a hull
change but only moves the marker on a row change).

**The table publishes only non-null rows.** The DataGrid clears its selection whenever a new page arrives. If that
null reached the shared selection, paging through the table would erase the chart marker. The table ignores null
selections, and after loading a page it re-highlights the shared row if that row is on the page. Records compare
by value, so a row from a fresh response equals the selected one.

**The chart ViewModel has plain state plus LiveCharts objects.** `Curve` (a list of `CurvePoint`) and `Marker` are
the state that tests check. The series, axes and the dashed marker line (a `RectangularSection` with Xi = Xj) are
created once in the constructor and updated from that state in `OnCurveChanged` / `OnMarkerChanged`. LiveCharts
types appear only in the chart ViewModel. Keeping them out entirely would need a mapping layer in the view, which
is more code for the same result.

**Selecting a row never calls the API.** The marker uses the row's own draft and displacement. A test checks that
the curve request count stays at 1. A new hull clears the old curve immediately, so the previous hull's curve is
never shown under the new hull's name, then loads the new one (with the same cancel-the-previous-request pattern
as the table).

**The chart has its own error and Retry.** If the curve fails to load, the chart shows the message and a Retry
button bound to `LoadCurveCommand`, separate from the table's banner. Each panel recovers on its own.

**Short animations.** `AnimationsSpeed` is 150 ms, so the marker moves "immediately" but visibly.

## Phase 5: hull side view

**A custom `Control` that overrides `Render`, not a Canvas of shapes.** `HullProfileView` draws with
`DrawingContext` (rectangles, lines, text). One class owns the whole drawing, and nothing needs creating or
positioning as child elements. `AffectsRender` in the static constructor lists the properties that trigger a
redraw (Length, Depth, Draft, Foreground). A resize triggers one automatically, because `Render` reads
`Bounds.Size` each time.

**The layout math is separate from the drawing.** `HullProfileLayout.Create(size, L, D)` computes one uniform
scale (pixels per metre) as `min(freeWidth / L, freeHeight / D)`, centres the hull, and converts a draft to a
screen y. It is pure math, so tests check fit, margins, centring, aspect ratio at several sizes, and waterline
position without rendering anything. The control just draws the rectangles it is given.

**True aspect ratio, so the hulls look thin.** All three sample hulls are about 10:1 (length to depth), so the
side view is a long, shallow strip. That is correct for a barge, and the brief asks for the correct aspect ratio.
Exaggerating the vertical scale is common in naval-architecture plots, but it would need explaining in a demo.

**Theme-aware drawing.** The control adds itself as an owner of `TextElement.ForegroundProperty`, so it
inherits the theme's text colour. Outline, dimension lines and labels use it and work in light and dark mode.
Water uses fixed translucent blues that read well on both backgrounds.

**Label placement.** The draft caption sits above the hull, never inside it: the hull can be only a few pixels
tall. "WL" sits just above its line and "BL" just below its line, so at shallow drafts they don't overlap (the
first version did).

**`HullViewModel` has no API calls.** It copies the hull's L and D and the selected row's draft from
`SelectionService`. `Draft` is `double?`, and null means "no row selected": no waterline, a hint instead.

**Checking the rendering headlessly.** During development the real MainWindow was rendered to PNG with
`Avalonia.Headless` (Skia) against the running API, to check the drawing at several sizes and in the dark theme.
That was a throwaway scratch tool, not part of the repo. One finding: LiveCharts redraws on its own throttled
timer, so a capture taken immediately after a selection can show the chart mid-update. That is a capture artifact,
not an app bug.
