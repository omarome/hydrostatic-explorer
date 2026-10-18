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
Swagger UI is at **http://localhost:5080/swagger** (Development only, which is the default for `dotnet run`).

Then start the desktop app in a second terminal:

```bash
dotnet run --project src/HydroExplorer.App
```

The app reads the API address from `src/HydroExplorer.App/appsettings.json` (`Api:BaseUrl`, default
`http://localhost:5080/`). If the API isn't running, the app shows an error banner with a **Retry** button.
Start the API and click Retry.

### Using the app

- **Hull**: pick a hull in the top bar. The table reloads at page 1.
- **Paging**: ⏮ ◀ ▶ ⏭ buttons below the table, plus "Rows per page" (25 / 50 / 100). ⟳ reloads the current page.
- **Sorting**: click the **T**, **Δ** or **KM** column header to sort the whole table on the server. Click
  again to reverse. The ▲/▼ arrow shows the current sort.

## API reference

| Endpoint | Returns |
|---|---|
| `GET /api/hulls` | All hulls |
| `GET /api/hulls/{id}/hydrostatics` | One page of the hydrostatic table (`PagedResult<HydrostaticRow>`) |
| `GET /api/hulls/{id}/hydrostatics/curve` | Evenly sampled `(draft, displacement)` points for the chart |

Query parameters for `/hydrostatics`:

| Parameter | Default | Allowed |
|---|---|---|
| `page` | `1` | 1 or more. A page past the end returns `items: []` with correct totals. |
| `pageSize` | `25` | 1–200 |
| `sortBy` | `draft` | `draft`, `displacement`, `km` (case-insensitive) |
| `sortDir` | `asc` | `asc`, `desc` |
| `minDraft`, `maxDraft` | none | Optional draft range in metres, inclusive |

`/curve` takes `points` (default 100, allowed 2–1000).

Invalid parameters return **400** with a ProblemDetails body that names the bad parameter. An unknown hull id returns **404**.

Each `/api` request is delayed by **200 ms** so that loading states are visible in the app. To change the delay,
edit `SimulatedLatency:Milliseconds` in `src/HydroExplorer.Api/appsettings.json` (0 turns it off). The change
applies without a restart.

### curl examples

With the API running:

```bash
# All hulls
curl http://localhost:5080/api/hulls

# First page (25 rows) for the Large Pontoon. totalCount 1191, totalPages 48
curl "http://localhost:5080/api/hulls/3/hydrostatics"

# Last page: 16 rows, drafts 11.85 to 12.00
curl "http://localhost:5080/api/hulls/3/hydrostatics?page=48"

# 50 rows per page, sorted by KM, smallest first
curl "http://localhost:5080/api/hulls/3/hydrostatics?pageSize=50&sortBy=km&sortDir=asc"

# Only drafts from 3.00 m to 3.49 m (50 rows)
curl "http://localhost:5080/api/hulls/3/hydrostatics?minDraft=3&maxDraft=3.49&pageSize=100"

# 10 points on the displacement curve
curl "http://localhost:5080/api/hulls/1/hydrostatics/curve?points=10"

# Errors: 400 (pageSize too large, unknown sortBy) and 404 (unknown hull)
curl -i "http://localhost:5080/api/hulls/3/hydrostatics?pageSize=500&sortBy=foo"
curl -i "http://localhost:5080/api/hulls/99/hydrostatics"
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
