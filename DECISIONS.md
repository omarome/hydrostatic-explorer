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
to set up on a fresh machine. That's fine for a localhost app.

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
Exaggerating the vertical scale is common in naval-architecture plots, but it would need explaining to every viewer.

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

## Phase 6: two-way linking and polish

**The shared selection is now a draft, not a row.** In Phase 4 the table was the only source of selections,
so sharing the selected *row* worked. The new sources (chart click, slider, keyboard) produce a draft, and that
draft's row may be on a page the table hasn't loaded. Keeping "row" as the shared state would make the chart and
slider wait for a network round-trip before anything moved. So `SelectionService` now holds `SelectedDraft`
(a `double?`):

- **Any view can set it instantly.** `SelectDraft(x)` snaps x to the table's 1 cm grid, within 0.10 m … D.
- **The chart and hull view follow immediately.** The marker's displacement is read off the already-loaded
  curve by linear interpolation (`CurveInterpolator`, exact for a box hull).
- **The table treats it as a request.** If the row is on the current page it highlights it (no request).
  Otherwise it asks the API for the page that contains it.

**Exact draft equality is safe.** `SnapToTableDraft` and the table generator compute drafts with the same
expression (`Math.Round(cm / 100.0, 2)`), and JSON round-trips doubles exactly, so the table can match a row
with `==`. A test checks that a snapped draft equals a row's draft bit for bit.

**`page-of` endpoint.** `GET /api/hulls/{id}/hydrostatics/page-of?draft=&pageSize=&sortBy=&sortDir=` returns
an ordinary `PagedResult` for the page containing the snapped draft. It takes the table's sort order because
"which page" depends on it: 3.25 m is on page 13 sorted ascending and page 36 sorted descending. It reuses
`HydrostaticsQuery`'s sorting so the two endpoints can't disagree. A draft above D or below 0.10 m snaps to the
last or first row; a negative or non-finite draft is a 400.

**Two race guards in the table.** (1) As before, a superseded page response is ignored. (2) New: if the
selection moves to a row on the current page while a `page-of` request is in flight, that request is cancelled.
Otherwise its late response would replace the page and the highlighted row would vanish. A test covers each,
and removing either guard makes its test fail.

**Chart: click and drag, not hover.** The brief allows "clicking or hovering". Hover-to-select would change the
selection, and often the table's page, every time the mouse crossed the chart on its way somewhere else.
Click-and-drag gives the same scrubbing feel on purpose. The pixel-to-draft conversion
(`CartesianChart.ScalePixelsToData`) needs the control, so it lives in the chart's code-behind; the ViewModel
receives only the draft.

**Slider without feedback loops.** The slider's value is bound two-way to `HullViewModel.SliderDraft`. Moving
it calls `SelectDraft`. When the selection changes elsewhere, the ViewModel moves the slider while an
`_isUpdatingFromSelection` flag is set, so that change isn't sent back as a new selection. Without the flag, a
hull change (slider resets to 0.10 m) would wrongly select 0.10 m. The slider row is hidden until a hull is loaded.

**Keyboard through a tunnelling handler.** The table view registers its KeyDown handler with
`RoutingStrategies.Tunnel`, so it runs before the DataGrid's own key handling. ↑/↓ call
`MoveSelectionAsync(±1)`, which continues onto the next or previous page at a page edge and stops at the ends of
the table. PageUp/PageDown run the page commands. The logic is in the ViewModel (tested), and the code-behind
only maps keys. Programmatic selections scroll into view with `DataGrid.ScrollIntoView`.

**Status bar fed by the API client.** `HydroApiClient` times each request and reports success or failure to an
`ApiStatus` singleton. `StatusBarViewModel` formats it. No ViewModel has to remember to update the status bar,
and every request, including the curve and `page-of`, counts.

**URLs are always formatted with InvariantCulture.** This machine's locale writes 3.25 as "3,25". The page and
curve requests only had integers, so Phase 3 didn't notice; `page-of` sends a draft, which would have broken.
A test sets the culture to sv-SE and checks the URL.

**One theme.** Fluent theme with a single accent colour (#1E88E5, the chart's blue) for light and dark, so the
selected row, slider and chart match. Shared styles (`panel`, `panel-title`, `muted`, `error`, status dots) live
in App.axaml instead of being repeated in each view. Light and dark both follow the macOS setting.

**Table columns fit the window.** Star widths weighted by content width (like flex-grow), a `MinWidth` so a
narrow window scrolls sideways instead of squashing numbers, and units on a second header line. At the default
1440 × 900 window all ten columns are visible. The paging bar is a `WrapPanel`, so at the minimum width the
page-size selector moves onto a second line instead of being clipped.

**Headless render checks.** The scratch tool from Phase 5 was extended to drive the real window with simulated
input (`MouseDown` on the chart, `KeyPress` on the grid, a slider move) against the running API, and to
capture PNGs in light, dark, minimum size and API-down states. That checked the code-behind paths that ViewModel
tests can't reach. It stays outside the repo.

## Stretch: isometric view and CSV export

**Isometric view as a second custom control, not a 3D engine.** `HullIsometricView` uses the same approach as
the side view: StyledProperties marked `AffectsRender` and a `Render` override. The projection
(`u = (x − y)·cos30°`, `v = z + (x + y)·sin30°`, one scale on all axes) lives in `HullIsometricLayout`, which is
pure math and unit-tested like `HullProfileLayout`. The box is drawn as a "glass box" (hidden edges dashed,
submerged faces shaded, translucent waterplane) instead of solid faces, so the waterplane inside stays visible
and no painter's-algorithm sorting is needed. The two views share a tab instead of splitting the panel, so each
keeps a useful size. Colours and the label helper moved to a shared `HullDrawing` class.

**Wider left margins.** Headless renders showed the "D = 12 m" label clipped at the left edge whenever the
hull's width limits the scale (the Large Pontoon in a normal-sized panel). This had been the case since Phase 5.
The side view's left margin went from 80 to 100 px; the isometric view uses 80 px.

**CSV export: formatting in Core, the dialog behind an interface.** `HydrostaticCsvWriter` (Core) turns rows into
text: InvariantCulture numbers (a comma decimal separator would break the columns), ASCII headers with units,
at most 6 decimals so floating-point noise like `43726.49999999999` is written as `43726.5`, and `\r\n` line
ends (RFC 4180). The ViewModel's `ExportCsvCommand` calls `IFileSaveService`, whose real implementation shows
Avalonia's async save dialog on the main window. Tests use `FakeFileSaveService` to check the file name, content,
Cancel and write errors without a window. It exports the current page only, as the brief says. Exporting the
whole table would mean fetching every page, or adding a CSV endpoint to the API.

## Stretch: single-palette styling

**One `ResourceDictionary` with `ThemeDictionaries`, everything else `{DynamicResource ...}`.** Colours, raw
hex values included, had crept into `App.axaml` (`#1E88E5`, `#E53935`, `#43A047`, ...), `HydroChartViewModel`
(LiveCharts `SKColor`s), and the two hull controls (`Color.Parse` in `HullDrawing`/`HullIsometricView`). A
`style-audit` pass moved every one of them into `Styles/Palette.axaml`: raw `Color` resources (navy → ocean →
teal → aqua, plus success/error/neutral) at the top, then role-named `SolidColorBrush`es
(`AccentBrush`, `CurveBrush`, `WaterFillBrush`, `MarkerBrush`, ...) inside `ResourceDictionary.ThemeDictionaries`
for `Light` and `Dark`. Spacing/size resources (`PanelPadding`, `PanelCornerRadius`, `SmallFontSize`, ...) live
in the same file. `App.axaml` now only merges the palette and includes `Styles/AppStyles.axaml`, which holds the
control styles that used to sit inline in `App.axaml`.
WPF note: the same idea in WPF is a `ResourceDictionary` merged into `App.xaml.Resources` and referenced with
`DynamicResource`; WPF has no `ThemeDictionaries`, so a light/dark switch means swapping dictionaries at runtime
yourself (or using a third-party theming library).

**Custom controls get `StyledProperty<IBrush?>` water brushes, not static fields.** `HullProfileView` and
`HullIsometricView` used to share `static readonly IBrush` fields on `HullDrawing`, built once from hex strings
— which could never repaint on a theme switch even if the hex moved into a resource, because a static field is
read once. Each control now exposes its own `SeaBrush`/`SubmergedBrush`/`WaterlineBrush`/`WaterplaneBrush`
`StyledProperty`s (marked `AffectsRender`, like `Foreground`), set by a `controls|HullProfileView` /
`controls|HullIsometricView` style in `AppStyles.axaml` from the palette. `Render` treats a null brush as "not
styled yet" and skips that drawing step, rather than falling back to a hard-coded colour.

**Chart colours resolved in the view, not the ViewModel.** `HydroChartViewModel` built its LiveCharts
`SolidColorPaint`s from `SKColor` statics in its constructor — a ViewModel holding colours, which also meant the
chart never reacted to a theme change. The ViewModel now builds its `Series`/`Axes` uncoloured and exposes
`ApplyColors(SKColor curve, SKColor marker, SKColor markerOutline, SKColor axis)`. `HydroChartView`'s
code-behind resolves `CurveBrush`/`MarkerBrush`/`MarkerOutlineBrush`/`AxisBrush` from the palette
(`TryFindResource`, converting the resolved `ISolidColorBrush.Color` to an `SKColor`) and calls `ApplyColors`
from `OnDataContextChanged` and again on `ActualThemeVariantChanged`, so the chart repaints on a light/dark
switch without the ViewModel ever naming a colour.

## 3D ship, phase 1: hull form and numerical hydrostatics

**Simpson's rule.** It's the integration rule naval architects use on offsets tables ("Simpson's first rule"),
so the code reads like the textbook. It's exact for polynomials up to degree 3, which gives a strong test: on a
box (constant half-breadth) the integrator must reproduce every box formula to 1e-9. The integrator is run on
the box seed hulls directly for that test, since `HullShape` can't express the infinite exponents of a box.

**Analytic hull form, not an offsets table.** `y = B/2 · f(ξ) · g(z)` with power-law ends and sections. Three
numbers describe a believable hull, p = q = 2 is the Wigley hull with closed-form answers to test against, and
Cb has a closed form (`HullShape.FullDepthBlockCoefficient`). Each half of the ship is integrated separately
(midships is a station) because the fore and aft exponents differ, and Simpson's rule needs a smooth function
over each piece. That's why station counts must be 5, 9, …, 41, …

**Convergence test at 1e-4, not the plan's 1e-6.** Measured before writing the test: doubling the stations
changes the Coastal Tanker's volume by 8.5e-6, and doubling the waterlines by up to 2.5e-5. Its high exponents
(8 / 6 / 10) have large fourth derivatives, which is Simpson's error term. The Feeder converges to ~1e-16
along the length. 2.5e-5 is about 0.25 t on 10,000 t, so the default grid (41 × 21) was kept and the test
tolerance set to 1e-4, rather than doing 4× the work for ~1e-6.

**Awp only "never decreases" in the tests.** Near the deck the tanker's sides are nearly vertical (q = 10):
`(1 − T/D)^10` falls below 1e-15, so consecutive rows have equal Awp in double precision. Volume is still
strictly increasing.

**`Hull.Shape` as an optional last parameter.** Existing calls and JSON without `shape` still work; boxes
serialize `"shape": null`. `FullDepthBlockCoefficient` is a computed property, so it appears in the JSON too
(read-only, ignored when deserializing); that's handy for curl users and costs nothing.

**No caching yet.** The API still generates the whole table per request. Measured: a Container Feeder page
takes ~75–95 ms of computation (box barges ~3 ms), on top of the 200 ms simulated latency. That's fine for a
local app. If it ever matters, caching the generated table per hull (hulls are immutable) is simpler than the plan's
cumulative integration.

**Interim look in the app.** Until phase 3 the side and isometric views draw hulls 4 and 5 as boxes; the
table, curve and CSV already show the integrated values.

## 3D ship, phase 2: camera, mesh and shading

**Plain `System.Numerics`, no 3D library.** `OrbitCamera` builds the standard view and perspective matrices
with `Matrix4x4.CreateLookAt` / `CreatePerspectiveFieldOfView` and divides by w itself, so every step of the
pipeline can be explained and unit-tested. It returns Avalonia's `Point` and takes a `Size`, as the existing
`*Layout` classes do; those are plain value types, and nothing in `Rendering/` draws.

**Conventions.** World = hull coordinates (x to the bow, y to port, z up). Yaw 0 looks at the starboard side
with the bow to the right. Pitch is clamped to 5°–85°. The plan's test "yaw 90° maps +x onto the screen axis
+y used to be on" is written as rotation: a point on +x at yaw 90° lands on the same pixel as the matching
point on the y axis at yaw 0, because at yaw 0 the y axis points into the screen.

**Fit by bounding sphere.** `FitTo` backs off until the box's bounding sphere fills the narrower viewing angle
(`d = r / sin(halfAngle)`). A sphere looks the same from every side, so the model stays in view however it's
rotated (tested at several yaws). The cost: a long, narrow ship fills only about half the width. Phase 3 may
tighten the default zoom.

**`FaceShading` returns a brightness, not a colour.** Lambert `ambient + diffuse · max(0, n·l)` in [0.35, 1].
The control multiplies a palette colour by it, so colours stay in `Palette.axaml`. Normals use the cross product
of the diagonals, which works for the slightly non-planar quads on the curved hull. A quad with no area (where
the sides meet at the bow) gets a zero normal instead of NaN.

**Mesh: one grid row exactly at the draft.** Twelve height levels, with the draft inserted, or an existing level
moved onto it if it's within 1 % of a slice (no sliver faces). So every hull face is wholly below or wholly above
the water. The sides use `HullGeometry.HalfBreadth`, the same function the hydrostatics integrate, so the 3D
hull and the table describe the same hull. Ships need no bottom or end faces (the half-breadth is 0 at the keel
and the ends); box barges get a bottom, a transom and a flat bow. `MeshQuad` is a `readonly record struct`, so
sorting about a thousand faces per frame allocates nothing per face.

**Superstructure proportions** (decorative; fractions of L from the stern and of D above the deck): deckhouse
0.04–0.20 L, 0.5 D high, 80 % of the narrower deck width; bridge 0.12–0.20 L, 0.25 D higher; funnel 0.06–0.10 L
up to 0.9 D above the deck. A headless render of the mesh (scratch tool, outside the repo) confirmed the shape,
the waterline split and the culling before the control is written.

## 3D ship, phase 3: the Ship3DView control

**Draw order in its own class.** `Rendering/DrawOrder` culls back faces and sorts each group far to near
(painter's algorithm), so `Ship3DView.Render` only draws. The plan had this inside `Render`; as a class it is
unit-tested. Its lists are reused every frame, so sorting allocates nothing.

**Three passes are correct because the camera orbits the deck.** The target is at deck height amidships and
pitch is ≥ 5°, so the eye is always above z = D ≥ T (tested). Seen from above the water, everything below the
waterline is behind the water plane, so "below-water faces → water → above-water faces" needs no per-pixel
depth test.

**A tighter default view.** `OrbitCamera.Frame` starts from the safe sphere fit and corrects the distance until
the model fills about 90 % of the panel at the default angle (starboard bow quarter, yaw 30°, pitch 22°).
The framing includes the widest water (at deck level), so moving the slider doesn't re-zoom. Zoom is limited to
1.1–8 × the model's radius around the target, so the camera can never be inside the model and no point can be
behind it. The view re-frames when the hull changes, on double-click, and on resize until the user has moved it.

**A headlight instead of a fixed light.** `FaceShading.Headlight` lights from the camera's side, raised. With a
light fixed in the world, turning the ship showed its unlit side at ambient brightness only.

**Shaded colours.** Each face's palette colour is multiplied by its brightness, rounded to 32 steps, and the
brush and seam pen for each (part, below water, step) are cached, cleared when a palette brush changes (theme
switch). The seam pen, in the fill colour, hides the hairline gaps antialiasing leaves between neighbouring
faces. In the dark theme the faint face outlines (`MeshEdgeBrush`) are Navy at 25 %; near-black lines made the
Steel topside look like a lattice in a headless render.

**Pointer input everywhere in the control.** `Ship3DView` implements `ICustomHitTest`, so dragging works over
the empty water around the ship too; a control with no background is otherwise only hit where it has drawn.
It sets `ClipToBounds`, so a zoomed-in model doesn't paint over the panel.

**The water.** A translucent rectangle at z = T reaching 0.1 L past the hull, and the waterline drawn round the
hull from `HullGeometry.HalfBreadth`: the same curve the table's Awp integrates.

**Isometric view removed.** `HullIsometricView`, `HullIsometricLayout` and their tests were deleted (agreed)
once the 3D view replaced the tab; the shared `HullDrawing` helper stays for the side view and the 3D caption.
The palette's `WaterplaneBrush` is reused by the 3D water.

## Extra: a Service Operation Vessel (hull 6)

**Sources and assumptions.** Length 90 m and beam 19 m sit in the range of Ulstein's SOV/CSOV designs
(SX195 93 × 18 m, SX222/SX226 89.6 × 19.2 m; ulstein.com/vessels/csov-sov-vessel); Offshore Magazine gives
"about 80 m" and 80–90 persons on board. Neither publishes depth or draft, so D = 8 m is an assumption, as are
the exponents (bow 4, stern 5, section 6: Cb ≈ 0.70, a full stern for the aft working deck). The X-BOW shape,
cranes and daughter craft aren't modelled: the power-law hull form can't express an X-BOW.

**`SuperstructureLayout` in `HullShape`.** The SOV needs its superstructure forward, so the layout became data:
an enum `AftAccommodation | ForwardAccommodation` as an optional last parameter of `HullShape` (only ship hulls
have a superstructure). It's decorative; the hydrostatics ignore it. `[JsonConverter(typeof(JsonStringEnumConverter))]`
on the enum makes the API send `"ForwardAccommodation"` instead of `1`; System.Text.Json is part of .NET, so Core
still has no package dependencies. Choosing the layout in the app by hull id would have hidden the rule.

**The SOV model.** Three accommodation tiers (0.55–0.85 L, stepping in and back towards the top), the bridge
on top with a helideck slab overhanging forward, a gangway tower at 0.44–0.48 L with the gangway boom stowed aft
along the centreline, and nothing on the deck aft of 0.22 L. Two new mesh parts and palette roles:
`GangwayBrush` (Amber; gangways are painted safety yellow/orange) and `HelideckBrush` (Kelp; helidecks are
traditionally green). The fixed `SuperstructureHeightFactor` became `MaxHeightAboveDeck(shape)`, since the SOV's
superstructure (1.41 D, ~11 m) is taller than a coaster's funnel (0.9 D).

## 3D ship, phase 4: polish

**One superstructure definition for both views.** The boxes moved out of `ShipMeshBuilder` into
`Rendering/SuperstructureBoxes` (aft/forward accommodation, with `MaxHeightAboveDeck`). The 3D mesh turns each box
into five faces; the side profile draws each as a rectangle. A test checks that the mesh spans exactly the boxes.
`HullProfileLayout.Create` takes an optional `heightAboveDeck`, so the profile shrinks just enough to fit the
superstructure (default 0: the existing layout tests are unchanged).

**Smoothness, measured.** A headless timing of `Ship3DView.Render` during simulated drags (Release, 900 × 600):

| Hull | Before | After |
|---|---|---|
| Large Pontoon | 11.5 ms, 3.5 MB/frame | 9.5 ms, 1.9 MB/frame |
| Container Feeder | 6.2 ms, 2.7 MB/frame | 4.1 ms, 1.4 MB/frame |
| Service Operation Vessel | 4.9 ms, 2.5 MB/frame | 3.5 ms, 1.3 MB/frame |

Changes: the camera builds its view and projection matrices once per frame instead of per point (they're cached
until yaw, pitch, distance, target or viewport changes); the outline and waterline pens are cached with the shaded
brushes; faces use `StreamGeometry` instead of `PolylineGeometry`. What's left is mostly one geometry object per
face per frame, which Avalonia's `DrawingContext` requires: faces must be drawn one at a time in depth order, so
they can't be batched by colour. Short-lived Gen0 objects are cheap for .NET's GC, and every case is well inside
a 60 fps frame. The pontoon is slowest because a box shows more faces at once (flat ends and more height slices).

**Body plan (optional extra, agreed).** `BodyPlanView` draws the sections at stations 1–9 of 10 from
`HullGeometry.HalfBreadth`: aft half left, forward half right, midships on both and drawn heavier, the waterline
across. Stations 0 and 10 are left out: a ship has no width there. Section and scaling math is in
`BodyPlanLayout`, tested like the other layouts. The caption sits at the top edge, as in the 3D view, so it can't
collide with the "CL" label.

