---
description: Build the 3D ship hull feature one phase at a time (see SHIP_3D_PLAN.md)
argument-hint: <phase number 0-4, or "status">
---
Work on the 3D ship feature. Phase: $ARGUMENTS

1. Read `SHIP_3D_PLAN.md` and `CLAUDE.md`. If the argument is "status" or empty, report which boxes are ticked,
   what the next phase is, and stop.
2. Do **only** the requested phase. If an earlier phase has unticked boxes, say so and ask before continuing.
3. Before writing code, list the files you'll create or change and any deviation from the plan, and wait for my OK.
4. Write tests first for the math (Core and App/Rendering), then the implementation. Follow the plan's formulas;
   if you think one is wrong, stop and explain rather than silently changing it.
5. Rules from CLAUDE.md apply: no new NuGet packages, no colours outside `Styles/Palette.axaml`, no Avalonia types
   in ViewModels, InvariantCulture, keep the 1 cm draft grid.
6. Run `dotnet build` and `dotnet test` until both are clean.
7. Tick the finished boxes in `SHIP_3D_PLAN.md` and add short entries to DECISIONS.md for any choices you made.
8. Finish with: what changed, test count, and (for phases 3–4) exact steps to check it in the running app.
   Don't commit; suggest a commit message instead.
