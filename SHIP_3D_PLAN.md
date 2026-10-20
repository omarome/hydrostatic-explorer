# Plan: 3D ship hull with real hydrostatics

Goal: replace the box-only hull with ship-shaped hulls whose **table, curve and 3D view all agree**, and
replace the isometric tab with an interactive, shaded 3D view of the whole ship (hull + decorative
superstructure). No new NuGet packages. Built in phases with `/ship-3d <phase>`; tick the boxes as you go.

## Decisions (agreed)
- **Real math:** ship hulls get hydrostatics by numerical integration (Simpson's rule) over an analytic hull form.
  Box barges keep their exact formulas. The integrator must reproduce the box formulas exactly (that's the main test).
- **Custom 3D:** our own projection and painter's-algorithm drawing in an Avalonia custom control
  (`System.Numerics` for vectors/matrices). No OpenGL, runs on macOS, all math unit-tested.
- **Hull + superstructure:** deckhouse, bridge and funnel are decorative boxes. They don't affect hydrostatics.

## The hull form

Coordinates as today: x along the length (0 = aft, L = fore), y across (centreline 0, half-breadth ±),
z up from the keel (0..D).

Half-breadth at (x, z):

```
y(x, z) = B/2 · f(ξ) · g(z)

ξ = (x − L/2) / (L/2)              −1 at the stern, 0 amidships, +1 at the bow
f(ξ) = 1 − |ξ|^pFore   for ξ ≥ 0   (waterline shape: bigger p → fuller ends, longer parallel midbody)
f(ξ) = 1 − |ξ|^pAft    for ξ < 0
g(z) = 1 − (1 − z/D)^q             (section shape: bigger q → flatter bottom, more vertical sides)
```

- p = q = 2 is the classic **Wigley hull**, which has analytic results to test against.
- p, q → ∞ tends to the box. The box itself stays a separate, exact case.
- Block coefficient at full depth: Cb = mean(f) · mean(g) = ½(pF/(pF+1) + pA/(pA+1)) · q/(q+1).

New Core type: `HullShape(double BowExponent, double SternExponent, double SectionExponent)`.
`Hull` gets an optional last parameter `HullShape? Shape = null` (null = box), so existing code and JSON keep working.

Seed hulls (keep ids 1–3 unchanged so the README demo script still holds):

| Id | Name | L × B × D (m) | pFore / pAft / q | ≈ Cb |
|---|---|---|---|---|
| 4 | Coastal Tanker | 100 × 16 × 8 | 8 / 6 / 10 | 0.79 |
| 5 | Container Feeder | 140 × 22 × 11 | 3 / 3 / 5 | 0.63 |

## Hydrostatics by integration (ship hulls)

Stations along x: an odd count (default 41), with **midship as a station** so each half is integrated separately
(f has a different exponent fore and aft). Waterlines from 0 to T: an odd count (default 21). Simpson's rule throughout.

```
Awp(T)  = 2 ∫ y(x,T) dx                       waterplane area
LCF     = 2 ∫ x·y(x,T) dx / Awp               longitudinal centre of flotation
∇(T)    = ∫₀ᵀ Awp(z) dz                       volume
KB      = ∫₀ᵀ z·Awp(z) dz / ∇
IT      = (2/3) ∫ y(x,T)³ dx                  transverse 2nd moment of the waterplane
IL      = 2 ∫ (x − LCF)²·y(x,T) dx            longitudinal 2nd moment about LCF
BM = IT/∇, KM = KB + BM, BML = IL/∇, Δ = ρ∇, TPC = Awp·ρ/100, MCT = Δ·BML/(100·L)   (GML ≈ BML, as today)
```

`HydrostaticsCalculator.Calculate` keeps its signature and dispatches: `Shape is null` → box formulas,
otherwise → `NumericalHydrostatics`. `HydrostaticRow` is unchanged, so API, table, chart and CSV need no changes.
(LCB/LCF columns are a possible later extra; not in scope.)

Performance: the largest table is ~1,200 rows; the integration is ~1k evaluations per row, so well under a
second. If it isn't, integrate cumulatively over drafts instead of from 0 for every row.

## 3D rendering

`src/HydroExplorer.App/Rendering/` (pure C#, no Avalonia drawing, fully unit-tested):
- `OrbitCamera`: yaw, pitch (clamped 5°–85° so the camera stays above the water), distance; view + perspective
  projection with `Matrix4x4`; `Project(Vector3) → Point?` (null if behind the camera); `FitTo(bounds, viewport)`.
- `ShipMeshBuilder`: builds quads (4-point faces with a part tag) from the hull geometry:
  hull sides (both sides, stations × waterlines, with **a waterline row exactly at the current draft**), deck,
  and for ship hulls the superstructure boxes (deckhouse ~0.04L–0.20L aft, bridge on top, funnel).
  Each face knows whether it is below or above the waterline.
- `FaceShading`: Lambert shading, `colour × (ambient + diffuse · max(0, n·light))`, back-face culling helper.

`src/HydroExplorer.App/Controls/Ship3DView.cs` (custom `Control`, replaces `HullIsometricView`):
- StyledProperties: `Length, Beam, Depth, Shape, Draft` (+ `AffectsRender`), plus brush properties set from the
  palette by a style in `AppStyles.axaml`.
- `Render`: build mesh (cache it; rebuild only when hull or draft changes), transform, cull back faces, sort by
  depth (painter's algorithm), draw in three passes: **below-water faces → translucent water plane at z = T
  (extends past the hull) → above-water faces**. Thin outline on hull faces at low opacity.
- Interaction (view glue, inside the control): drag = rotate, wheel = zoom, double-click = reset view.
- Colours: new palette roles in `Palette.axaml` (both themes): `HullTopsideBrush`, `HullBottomBrush`
  (classic antifouling red, add a raw colour `Rust`), `DeckBrush`, `SuperstructureBrush`, `FunnelBrush`, plus
  existing `WaterFillBrush`, `WaterlineBrush`. No hex outside the palette.

The HullView tabs become **3D** (default) and **Side**. The side profile gets the superstructure outline for ship
hulls; the slider and selection flow are unchanged.

## Phases

### Phase 0: safe starting point
- [x] Ask me to commit the current palette/style work (it's uncommitted). Then `dotnet build` + `dotnet test` green.

### Phase 1: hull form + numerical hydrostatics (Core, API)
- [x] `HullShape` record; `Hull.Shape` optional parameter; XML docs.
- [x] `HullGeometry.HalfBreadth(hull, x, z)` and a reusable `Simpson.Integrate(...)`.
- [x] `NumericalHydrostatics.Calculate(hull, draft)`; `HydrostaticsCalculator` dispatches on `Shape`.
- [x] Tests: integrator on a box-equivalent form equals the box formulas for every field (1e-9 relative);
      Wigley: ∇(D) = 4/9·L·B·D, Awp(T) = (2/3)·L·B·g(T), KB analytic, IT with ∫(1−ξ²)³ = 32/35 (1e-4);
      Cb at full depth matches the formula above (1e-3); ∇ and Awp increase with draft; doubling stations
      changes ∇ by < 1e-6 relative.
      *Done with two agreed changes (see DECISIONS.md): convergence tolerance 1e-4 (tanker measured ~1e-5),
      and Awp "never decreases" (equal in double precision near the tanker's deck).*
- [x] Seed hulls 4 and 5; update `SeedHullRepositoryTests`. API returns `shape` in hull JSON; API test for it.
- [x] README (hull list, API sample), DECISIONS.md (why Simpson, why this form), CLAUDE.md ("box = exact, ship = integrated").

### Phase 2: 3D math (App/Rendering)
- [x] `OrbitCamera`, `ShipMeshBuilder`, `FaceShading` as above.
- [x] Tests: camera target projects to the viewport centre; yaw 90° maps +x onto the screen axis +y used to be on;
      points behind the camera return null; pitch is clamped; `FitTo` keeps all mesh vertices inside the viewport.
      Mesh: every vertex inside the L × B × (D + superstructure) bounds; a waterline row exists at exactly T;
      faces split correctly into below/above; box hull gives flat sides; superstructure only for ship hulls.

### Phase 3: Ship3DView control
- [ ] Control, palette roles, style in `AppStyles.axaml`, tab swap in `HullView.axaml` (3D default).
- [ ] `HullViewModel` exposes `Shape`; ViewModel test.
- [ ] Run the app; tell me exactly what to check (rotate, zoom, reset, slider moves the water, hull switch,
      light and dark mode). Fix what I report.
- [ ] Ask before deleting `HullIsometricView`, `HullIsometricLayout` and their tests.

### Phase 4: polish and story
- [ ] Side profile: superstructure outline for ship hulls.
- [ ] Smooth feel: mesh cache, no allocations in hot paths; check dragging stays fluid on the Large Pontoon and Feeder.
- [ ] README demo script: add a 3D step (pick Container Feeder, rotate, drag the draft slider).
- [ ] Optional extra (ask first): a "Body plan" tab drawing the sections, a classic naval-architecture view.
