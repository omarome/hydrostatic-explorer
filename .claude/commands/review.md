---
description: Review the current changes against this project's rules
---
Review the uncommitted changes (`git diff` and `git diff --staged`; include new untracked files from `git status`). $ARGUMENTS

Check against CLAUDE.md, especially:
- MVVM: Avalonia types, dialogs or `Window` in ViewModels; logic in code-behind that belongs in a ViewModel.
- Async: `async void` outside event handlers, `.Result` / `.Wait()`, missing cancellation or stale-response handling on loads.
- Numbers: drafts built by repeated addition, missing `SnapToTableDraft`, culture-sensitive formatting in URLs/CSV.
- Views: missing `x:DataType`, WPF-only syntax in `.axaml`.
- Styling: colour literals or repeated sizes outside `Styles/Palette.axaml`, colours in ViewModels, inline setters that should be a style class.
- API: missing validation → 400 ProblemDetails, missing 404, README not updated.
- Tests: new behaviour without a test.
- Anything I'd struggle to explain in an interview: unclear names, missing "why" comments, needless cleverness.

Run `dotnet build` and `dotnet test`. Report findings as a short list, most important first, each with file:line and a suggested fix. Don't edit files unless I ask.
