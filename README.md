# Hydrostatics Explorer

A small desktop demo app: hydrostatic data for a box-shaped hull, shown in three connected views
(table, curve, hull side view). The data comes from a local REST API with server-side paging.

- **HydroExplorer.Core**: domain model and hydrostatics math (no UI, no HTTP)
- **HydroExplorer.Api**: ASP.NET Core minimal API serving hulls and hydrostatic tables
- **HydroExplorer.App**: Avalonia 11 MVVM desktop app (runs on macOS, Windows, Linux)

Further reading:

- [ARCHITECTURE.md](ARCHITECTURE.md): projects, data flow, how the selection is shared, React → Avalonia/WPF concept map
- [DECISIONS.md](DECISIONS.md): every design choice and why, phase by phase

## Prerequisites

- .NET 8 SDK (`dotnet --list-sdks` should show `8.0.x`). `global.json` pins the solution to SDK 8.0.x
  (any 8.0 feature band). Install it from https://dotnet.microsoft.com/download/dotnet/8.0, or without admin rights:
  `curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0`, then add `~/.dotnet` to `PATH`.
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

The table, chart and hull view are three views of one shared selection: a hull and a draft.
Any of them can change it, and the other two follow.

- **Hull**: pick a hull in the top bar. The table reloads at page 1, and the chart and hull view redraw.
- **Table**: click a row to select its draft. ⏮ ◀ ▶ ⏭ change page, "Rows per page" sets 25 / 50 / 100, and
  ⟳ reloads. **Export CSV** saves the rows on the current page to a file you choose. Click the **T**, **Δ** or **KM** header to sort the whole table on the server (click again to reverse).
- **Keyboard** (with the table focused): **↑/↓** move the selection one row and continue onto the next or
  previous page at the page edge. **PageUp/PageDown** change page.
- **Chart**: displacement against draft. **Click or drag** on it to select the nearest draft. If that row
  is on another page, the table jumps to it.
- **Hull view**: two tabs. **Side** shows the side profile at its true proportions, with the waterline (WL) at
  the selected draft and the submerged part shaded. **Isometric** shows the hull box (L × B × D) in pseudo-3D
  with the waterplane at the selected draft. Drag the **Draft** slider under either one to scrub through drafts.
- **Status bar**: green when the API answered the last request, red (with the reason) when it didn't, plus the
  time and duration of the last successful load.

Every selection snaps to the table's 1 cm grid (for example a click at 3.2547 m selects 3.25 m).

## Demo script (2 minutes)

Before the demo, start the API and the app (see [Run](#run)) and leave both terminals visible.
The app opens on the **Small Barge** (40 × 10 × 4 m, 391 rows, 16 pages).

| Time | Do this | Point out |
|---|---|---|
| 0:00 | Nothing yet | Three views of one model: table (left), curve (top right), hull side view (bottom right). Green status bar: connected, last load ≈ 200 ms (simulated latency). |
| 0:10 | Click the **0.20** row in the table | Orange marker and dashed line jump to 0.20 m on the chart, the waterline moves, the slider follows. No request was made: the status bar's last load time doesn't change. |
| 0:25 | Click the chart near **T = 3 m** | The table jumps to the page holding that draft (page 12 of 16 for 3.00 m) and highlights the row: one `page-of` request. Marker and waterline move. |
| 0:40 | Drag the **Draft** slider slowly left and right | Waterline and marker follow instantly. The table catches up, fetching a page only when the draft leaves the current one. |
| 0:55 | Click a row, then press **↓** repeatedly past the bottom of the page, then **PageUp** | The selection steps across the page boundary onto the next page. PageUp goes back a page. |
| 1:10 | Click the **Δ** header twice, then set **Rows per page** to 100 | Server-side sort (the ▲/▼ arrow flips; page 1 now starts at 4.00 m). The page count drops to 4. |
| 1:25 | Pick **Large Pontoon** in the Hull box | Selection clears, curve and hull redraw (120 × 12 m, still to scale), 1,191 rows. |
| 1:35 | In the API terminal press **Ctrl+C**, then click **▶** in the app | Red error banner with Retry; status bar turns red and says why. The table keeps the last good page. |
| 1:50 | Restart the API (`dotnet run --project src/HydroExplorer.Api`), click **Retry** | Data reloads, banner disappears, status bar turns green. |

## API reference

| Endpoint | Returns |
|---|---|
| `GET /api/hulls` | All hulls |
| `GET /api/hulls/{id}/hydrostatics` | One page of the hydrostatic table (`PagedResult<HydrostaticRow>`) |
| `GET /api/hulls/{id}/hydrostatics/page-of?draft=` | The page containing the row nearest to `draft` (`PagedResult<HydrostaticRow>`) |
| `GET /api/hulls/{id}/hydrostatics/curve` | Evenly sampled `(draft, displacement)` points for the chart |

Query parameters for `/hydrostatics`:

| Parameter | Default | Allowed |
|---|---|---|
| `page` | `1` | 1 or more. A page past the end returns `items: []` with correct totals. |
| `pageSize` | `25` | 1–200 |
| `sortBy` | `draft` | `draft`, `displacement`, `km` (case-insensitive) |
| `sortDir` | `asc` | `asc`, `desc` |
| `minDraft`, `maxDraft` | none | Optional draft range in metres, inclusive |

`/page-of` takes `draft` (required, metres, ≥ 0) plus the same `pageSize`, `sortBy` and `sortDir`, so the page
matches what the table shows. The draft snaps to the nearest table row: whole centimetres, within 0.10 m … D.

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

# Which page holds draft 3.25 m? Page 13 ascending (page 36 with sortDir=desc)
curl "http://localhost:5080/api/hulls/3/hydrostatics/page-of?draft=3.25"

# 10 points on the displacement curve
curl "http://localhost:5080/api/hulls/1/hydrostatics/curve?points=10"

# Errors: 400 (pageSize too large, unknown sortBy) and 404 (unknown hull)
curl -i "http://localhost:5080/api/hulls/3/hydrostatics?pageSize=500&sortBy=foo"
curl -i "http://localhost:5080/api/hulls/99/hydrostatics"
```

## Project layout

```
HydroExplorer.sln
Directory.Build.props          # settings shared by all projects (net8.0, nullable, warnings as errors)
global.json                    # pins the .NET SDK version
src/
  HydroExplorer.Core/          # Models/, Hydrostatics/ (formulas, table, curve), Paging/, Data/ (seed hulls)
  HydroExplorer.Api/           # Program.cs, Endpoints/, Queries/ (validation, filter/sort/page), Infrastructure/ (latency)
  HydroExplorer.App/           # Services/ (API client, selection, status), ViewModels/, Views/, Controls/ (hull drawing)
tests/
  HydroExplorer.Core.Tests/    # formulas, table generation, snapping, interpolation, paging
  HydroExplorer.Api.Tests/     # WebApplicationFactory: endpoints, paging, sorting, filtering, page-of, 400/404
  HydroExplorer.App.Tests/     # ViewModels with a fake API client, HTTP client with a stub handler, layout math
```
