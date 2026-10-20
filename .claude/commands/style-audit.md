---
description: Find hard-coded colours/sizes and move them into the single palette
argument-hint: [optional: folder or file to limit the audit]
---
Audit HydroExplorer.App styling against the "Styling: one source of truth" rules in CLAUDE.md. Scope: $ARGUMENTS (default: all of `src/HydroExplorer.App`).

`src/HydroExplorer.App/Styles/Palette.axaml` already exists and defines the target colour scheme (navy/ocean/teal/aqua). Keep its raw colour values; map every existing usage onto its role brushes (e.g. old blue accent → `AccentBrush`, chart line → `CurveBrush`, water → `WaterFillBrush`/`WaterlineBrush`, orange marker → `MarkerBrush`, green/red → `SuccessBrush`/`ErrorBrush`). Add a new role only if nothing fits. Use the role brushes' `Color` (and `Opacity`) for C# drawing and LiveCharts paints. Use `HeaderGradientBrush` + `OnHeaderBrush` for the top bar, and `WindowBackgroundBrush`/`PanelBackgroundBrush` for window and panels.

**Step 1: report (no edits yet).** Search `.axaml` and `.cs` files for:
- hex colours (`#RGB`, `#RRGGBB`, `#AARRGGBB`), `Color.Parse`, `Color.FromRgb/FromArgb`, `SKColor.Parse`, `SKColors.*`, `Colors.*`, `Brushes.*`, named colours like `Fill="Gray"`
- inline `FontSize`, `Padding`, `Margin`, `CornerRadius`, `BorderThickness` values that repeat or belong to a shared look
- colours held in ViewModels
List each hit with file:line, group identical values, and propose one role-named palette entry per meaning
(e.g. `#1E88E5` used for accent, chart curve and water → `AccentColor`, `WaterColor`, ... or one shared role if the meaning really is the same). Note alpha variants (e.g. `#331E88E5`) as derived entries.
Wait for my OK on the names.

**Step 2: migrate.**
1. Create or update `src/HydroExplorer.App/Styles/Palette.axaml`: `Color` + `SolidColorBrush` resources inside `ResourceDictionary.ThemeDictionaries` (Light and Dark), plus spacing/size resources. Feed the FluentTheme accent from the same colour so there is exactly one hex value per colour.
2. Move app-wide styles from `App.axaml` into `Styles/AppStyles.axaml`; `App.axaml` just merges the palette and includes the styles.
3. Replace every hit with `{DynamicResource ...}` or a style class.
4. C# drawing code: custom controls (`HullDrawing`, `HullIsometricView`, `HullProfileView`) get `StyledProperty<IBrush>` properties (with `AffectsRender`) whose values are set from the palette in `AppStyles.axaml`. Move chart colours out of `HydroChartViewModel` so the chart's paints are created from palette resources in the view layer, and keep ViewModel tests passing.
5. Run `dotnet build` and `dotnet test`. Then grep again: no colour literals may remain outside `Palette.axaml`.
6. Add a short entry to DECISIONS.md explaining the palette (and the WPF equivalent: a merged ResourceDictionary with `DynamicResource`).

Finish with what to check visually in both light and dark mode.
