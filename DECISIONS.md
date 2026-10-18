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
