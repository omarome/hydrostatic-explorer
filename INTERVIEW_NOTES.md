# Interview notes

Likely technical questions about this codebase, with short answers based on the actual code.
See [ARCHITECTURE.md](ARCHITECTURE.md) for the big picture and [DECISIONS.md](DECISIONS.md) for the reasons behind each choice.

---

### 1. How is MVVM applied here? Where does the logic live?

- **Views** (`Views/*.axaml`) are declarative XAML plus minimal code-behind for view-only glue: turning a
  header click into `SortByCommand`, routing keys, converting a chart pixel to a draft, scrolling a row into view.
- **ViewModels** (`ViewModels/`) hold all UI state and behaviour: paging, sorting, loading and error states,
  and reacting to the selection. They don't reference any view type, so they're unit-tested without a window.
- **Services** (`Services/`) are the API client, `SelectionService` and `ApiStatus`.
- **Domain** is in `HydroExplorer.Core`: formulas, paging, snapping. It's shared by the API and the app.

A test for "is this MVVM?": `HydroTableViewModelTests` drives paging, sorting, errors and keyboard
navigation with no UI at all.

### 2. How does data binding work? What does `[ObservableProperty]` do?

Views bind to properties on their `DataContext` (the ViewModel). For the UI to update, the ViewModel must
implement `INotifyPropertyChanged` and raise `PropertyChanged` when a value changes. `ViewModelBase` inherits
CommunityToolkit's `ObservableObject`, and `[ObservableProperty] private int _currentPage;` makes a source
generator write the public `CurrentPage` property, the equality check, the `PropertyChanged` event, and
`partial void OnCurrentPageChanged` hooks. `[NotifyPropertyChangedFor(nameof(PageSummary))]` also notifies
computed properties.

Bindings are **compiled** (`AvaloniaUseCompiledBindingsByDefault` + `x:DataType`), so a typo in a binding path
fails the build. WPF bindings use reflection at runtime: a typo there only produces an output-window warning.

### 3. How do commands and their enabled state work?

`[RelayCommand(CanExecute = nameof(CanGoForward))] private Task NextPageAsync()` generates `NextPageCommand`,
an `IAsyncRelayCommand`. A Button's `Command` binding calls `CanExecute` to decide whether the button is
enabled. `[NotifyCanExecuteChangedFor(nameof(NextPageCommand), …)]` on `CurrentPage`, `TotalPages` and
`IsLoading` re-evaluates it when those change. So "Next" is disabled on the last page and during a load.
An async command also refuses to run twice at once by default.

### 4. How do you load data asynchronously without freezing the UI or showing wrong data?

- `async`/`await` all the way. After an `await`, Avalonia's `SynchronizationContext` resumes on the UI thread, so
  ViewModel properties can be set directly. The app code has no `Dispatcher` calls.
- `IsLoading` drives the progress bar and disables paging buttons.
- **Latest request wins.** Each load cancels the previous one's `CancellationTokenSource`, checks after the
  `await` whether it was superseded, and only the latest request clears `IsLoading`
  (`HydroTableViewModel.LoadAsync`).
- **State changes only on success.** A failed "next page" leaves page 1 visible and labelled page 1, with an error
  banner and Retry. The API client maps every failure to `HydroApiException`, so ViewModels catch one type.
- These races are tested deterministically: the fake client holds responses and the tests release them out of
  order. Removing either guard makes a test fail.

### 5. Why server-side pagination, and how is the API designed?

The client never holds the whole table, which matters for real hulls with far more rows.

- `GET /api/hulls/{id}/hydrostatics?page&pageSize&sortBy&sortDir&minDraft&maxDraft` returns
  `PagedResult<T>` (`Items, Page, PageSize, TotalCount, TotalPages`). The server filters → sorts → pages, in that
  order, so page 2 sorted by KM is page 2 of the whole sorted table.
- Validation (`HydrostaticsQuery.Validate`) collects all errors and returns a 400 ProblemDetails naming each bad
  parameter. An unknown hull is a 404. A page past the end returns empty items with correct totals.
- `/page-of?draft=` answers "which page holds this draft, in this sort order?" for chart and slider selections.
- `/curve?points=100` gives the chart the whole curve in one small response instead of paging.

### 6. How are the three views kept in sync?

One `SelectionService` singleton holds `SelectedHull` and `SelectedDraft`, and it's injected into each ViewModel.
This is lifting state up in React terms. Any view writes to it, and the others react to `PropertyChanged`.
- The chart moves its marker, reading displacement off the loaded curve.
- The hull view moves its waterline and slider.
- The table highlights the row, or fetches its page via `page-of`.

Changing hull clears the draft inside the service, so no view sees a new hull with an old draft.

**Why not `WeakReferenceMessenger`?** A service keeps *state* (late subscribers can read it), has one place for
invariants, makes dependencies visible in constructors, and is trivial to create fresh in tests. The messenger's
weak references matter for short-lived ViewModels; ours all live as long as the app.

### 7. How does the custom hull control work? StyledProperty vs DependencyProperty?

`HullProfileView` derives from `Control` and overrides `Render(DrawingContext)`. `Length`, `Depth` and `Draft`
are `StyledProperty`s (`AvaloniaProperty.Register<HullProfileView, double>(…)`), so they can be bound, styled and
animated. `AffectsRender<HullProfileView>(…)` in the static constructor makes any change trigger a redraw, and a
resize redraws because `Render` reads `Bounds.Size`.

In WPF the same control would use `DependencyProperty.Register(name, typeof(double), typeof(Owner),
new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender))` and override `OnRender`.
The scaling math (one pixels-per-metre scale to keep the aspect ratio, centred inside margins) is in
`HullProfileLayout`, a pure class with its own tests.

The other two hull controls follow the same pattern: `BodyPlanView` with `BodyPlanLayout`, and `Ship3DView`
with its math in `Rendering/` (see #14). Colours reach all of them as brush `StyledProperty`s set by a style from
`Palette.axaml`, so a light/dark switch repaints them without code. `Ship3DView` adds pointer handling
(drag, wheel, double-click) and `ICustomHitTest`, so the empty area around the ship takes input too.

### 8. How is it tested?

- **Core (113 tests):** each formula against hand calculations, drift-free draft generation (exact equality on all
  1,191 drafts), snapping, interpolation, paging edge cases, CSV formatting, Simpson's rule, and the numerical
  hydrostatics against the box formulas and the analytic Wigley hull.
- **Api (39 tests):** `WebApplicationFactory<Program>` runs the real API in memory: paging metadata, sorting,
  filtering, `page-of` in both sort orders, 400/404 ProblemDetails, Swagger, and the latency middleware.
- **App (141 tests):** ViewModels with `FakeHydroApiClient` (can fail, or hold responses to test loading states
  and races); the real `HydroApiClient` against a stub `HttpMessageHandler`, including a Swedish-locale test
  proving URLs say `3.25`, not `3,25`; CSV export with `FakeFileSaveService`; layout math for the side view and
  body plan; and the 3D pipeline (camera, mesh, culling and draw order, shading) without a window.

The app was also checked end-to-end by rendering the real window headlessly against the running API with
simulated clicks and key presses. That was a scratch tool, outside the repo.

### 9. What did you do about floating-point issues?

- Table drafts are generated from integer centimetres (`cm / 100.0`), not by adding 0.01 repeatedly. Repeated
  addition gives 3.2499999999999747 instead of 3.25.
- `SnapToTableDraft` uses the same expression, so a draft from the chart or slider is bit-for-bit equal to a
  row's draft and can be matched with `==`.
- Values like displacement stay unrounded in the domain and API, and the UI formats them for display.
- URLs are formatted with `InvariantCulture`.
- On the physics side: MCT uses GML ≈ BML, valid for these hulls because BML (tens to hundreds of metres) is far
  larger than KG (a few metres); noted in the calculator.

### 10. What would you change for production, or when porting to WPF?

- **Real hulls** would come from an offsets table (measured half-breadths) instead of an analytic form. The
  integrator already works on any half-breadth function, so only `HullGeometry` would change. Add LCB/LCF columns,
  and compute GML from a real KG instead of GML ≈ BML.
- Use the Generic Host with `IHttpClientFactory` and logging. Cache generated tables in the API. Make
  `TotalCount` cheap for a database-backed source.
- If ViewModels become short-lived (tabs, dialogs), unsubscribe from `SelectionService` or switch to the messenger.
- **WPF port:** the ViewModels, services and tests move unchanged, because CommunityToolkit.Mvvm works in both.
  The views change: `StyledProperty` → `DependencyProperty`, style classes → keyed styles, compiled bindings →
  reflection bindings (check the output window for binding errors), tunnel handlers → `Preview*` events,
  `Dispatcher.UIThread` → `Application.Current.Dispatcher`, and the DataGrid / LiveCharts packages → their WPF
  editions.

### 11. How do you debug a WPF app? And this one?

**WPF, in the order I'd reach for things:**

1. **Binding errors first.** A wrong binding path doesn't crash. WPF prints a message to the Output window and
   the control just shows nothing, for example
   `System.Windows.Data Error: 40 : BindingExpression path error: 'Draftt' property not found on 'HydroTableViewModel'`.
   VS 2022 lists them in the **XAML Binding Failures** window. For one binding that's hard to figure out,
   `diag:PresentationTraceSources.TraceLevel=High` on that binding logs every step: which DataContext it found,
   which value it got, and whether conversion failed.
2. **The live UI.** **Live Visual Tree** and **Live Property Explorer** in Visual Studio, or **Snoop** (free,
   works on any WPF app), show the running element tree, each element's DataContext, its real property values
   and where each value comes from (local value, style, inherited). XAML Hot Reload lets you edit XAML while
   the app is running.
3. **The debugger.** Breakpoints in ViewModels, commands and code-behind. Turn on "break when thrown" for CLR
   exceptions, because WPF can swallow an exception thrown inside a binding or an event handler.
4. **Threading.** *"The calling thread cannot access this object because a different thread owns it"* means
   code touched the UI from a background thread. Fix it with `Dispatcher.Invoke`, or by letting `await` resume
   on the UI thread. Add global handlers so no exception gets lost: `Application.DispatcherUnhandledException`,
   `AppDomain.CurrentDomain.UnhandledException` and `TaskScheduler.UnobservedTaskException`.
5. **Layout, performance and memory.** For layout problems, temporarily give panels a coloured background or set
   `ShowGridLines="True"` on a Grid. For a slow UI, use the VS Performance Profiler's Application Timeline
   (time spent in layout and rendering). For memory leaks, compare memory snapshots; in WPF a common cause is an
   event subscription that's never removed and keeps a view alive.

**In this project (Avalonia):**

- Compiled bindings (`x:DataType`, on by default in the csproj) turn a wrong binding path into a **build error**,
  so the most common WPF bug can't reach runtime.
- `.WithDeveloperTools()` in `Program.cs` (Debug builds only) connects the app to **Avalonia Developer Tools**
  (visual tree, properties, styles, events; F12 to open). That is a separate app from AvaloniaUI, free for
  individuals under its Community Edition; it isn't needed to build or run this project.
  `.LogToTrace()` sends Avalonia's warnings to the debug output.
- In VS Code with the C# Dev Kit, **F5** starts the app under the debugger with breakpoints, as in Visual Studio.
- Because the logic is in ViewModels, many bugs are faster to reproduce as a failing xUnit test with
  `FakeHydroApiClient` than by clicking through the UI.
- The headless renders used during development (PNG snapshots of the real controls) are another option for
  drawing bugs in the custom controls.

**Short answer:** "First I check the Output window for binding errors, because that's the usual cause when a
value just doesn't appear. Then I use Live Visual Tree or Snoop to check the DataContext and the real property
values. Then breakpoints in the ViewModel, with break-on-thrown exceptions turned on. Because the logic is in
ViewModels, I can often reproduce the bug in a unit test instead."

### 12. How are a ship hull's hydrostatics computed? Why Simpson's rule?

For a ship hull there are no closed-form formulas, so `NumericalHydrostatics` integrates the half-breadth
y(x, z) the way naval architects do from an offsets table:

- **Along the length:** the waterplane area, LCF and second moments (Awp = 2∫y dx, IT = ⅔∫y³ dx,
  IL = 2∫(x − LCF)²·y dx).
- **Up from the keel:** volume and KB (∇ = ∫Awp dz, KB = ∫z·Awp dz / ∇).

Simpson's rule fits a parabola through each pair of strips. It's exact for polynomials up to degree 3, which
gives the key test: run on a box (constant half-breadth), the integrator must reproduce every box formula to 1e-9.
It's also checked against the analytic Wigley hull (to 1e-4) and for convergence when the grid is doubled.
`HydrostaticsCalculator.Calculate` keeps one signature and dispatches: box → exact formulas, ship → integration.
So the API, table, chart and CSV didn't change at all.

### 13. How is the ship hull shape defined?

`y(x, z) = B/2 · f(ξ) · g(z)` with `f = 1 − |ξ|^p` (waterline shape; p per half, bow and stern) and
`g = 1 − (1 − z/D)^q` (section shape). Three numbers give a believable hull: bigger exponents mean fuller ends and
flatter sections, p = q = 2 is the Wigley hull, and the block coefficient has a closed form, mean(f)·mean(g).
Each half is integrated separately, with midships as a station, because the exponents differ fore and aft.
One function, `HullGeometry.HalfBreadth`, feeds the hydrostatics, the 3D mesh, the waterline and the body plan,
so every view and every number describe the same hull.

### 14. How does the 3D view work without a 3D engine? Painter's algorithm vs a depth buffer?

All the math is plain `System.Numerics`, in `Rendering/`, and unit-tested:

- **Camera:** `OrbitCamera` builds a view matrix (`CreateLookAt`) and a perspective matrix, then divides by w.
- **Culling:** faces whose normal points away from the camera are dropped, about half of them.
- **Painter's algorithm:** `DrawOrder` sorts the rest far to near, so nearer faces paint over farther ones.
- **Shading:** each face is coloured by Lambert's law, `ambient + diffuse·max(0, n·l)`, with a light that
  follows the camera.

A GPU instead uses a depth buffer, which keeps the nearest depth per pixel. That's correct for any geometry,
including faces that pass through each other. The painter's algorithm is per face, so it can misorder
overlapping faces, but for one closed, convex-ish hull of about a thousand faces it's simple, testable and fast.

The water needs no depth test. The camera orbits a point on the deck with pitch ≥ 5°, so it's always above the
water, and three passes are enough: below-water faces, then the translucent water, then everything above it.

Why no GPU? No new packages, and it runs anywhere Avalonia runs. Measured at 3.5–9.5 ms a frame while dragging,
well inside 60 fps. For a real CAD-sized model (millions of triangles) you'd move to the GPU: Avalonia's OpenGL
control, or in WPF `Viewport3D` or a DirectX interop surface.

### 15. How did you add the SOV without breaking anything?

A Service Operation Vessel needs its accommodation forward, unlike the cargo ships, so the layout became data:
an enum `SuperstructureLayout` added as an **optional last parameter** of `HullShape`. Existing code and old JSON
(no `superstructure` field) still work, and `[JsonConverter(typeof(JsonStringEnumConverter))]` sends
`"ForwardAccommodation"` instead of `1`. The superstructure is a list of boxes (`SuperstructureBoxes`) that both
the 3D mesh and the side profile draw from, so the two views can't disagree, and tests pin that down. Choosing the
layout in the app by hull id would have hidden the rule in the UI.
