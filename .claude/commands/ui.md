---
description: Make a UI change (layout, look, behaviour) using the project's palette and styles
argument-hint: <what should change in the UI>
---
UI change for HydroExplorer.App: $ARGUMENTS

First read the "Styling: one source of truth" section of CLAUDE.md, `Styles/Palette.axaml`, `Styles/AppStyles.axaml` and the views involved. If `Styles/Palette.axaml` doesn't exist yet, stop and tell me to run `/style-audit` first.

Propose the change briefly (which views, which new palette roles or style classes if any) and wait for my OK. Then:
1. **Colours, sizes, spacing:** only from the palette via `{DynamicResource ...}`. If a value is missing, add a role-named entry to `Palette.axaml` (both Light and Dark) instead of hard-coding it.
2. **Styles:** reusable looks go in `AppStyles.axaml` as a class selector (e.g. `Border.card`). Views apply classes; no inline colour/size setters.
3. **Behaviour:** state and logic go in the ViewModel (`[ObservableProperty]`, `[RelayCommand]`), with a test in `tests/HydroExplorer.App.Tests`. Code-behind only for view glue.
4. Keep `x:DataType` on every view (compiled bindings).
5. Run `dotnet build` and `dotnet test`.
6. Grep your diff for `#[0-9A-Fa-f]{6}`, `Color.Parse`, `SKColor.Parse`, `Colors.` outside `Palette.axaml`; there must be none.

Finish with exact steps to check it in the running app, in **both light and dark mode** (macOS System Settings → Appearance).
