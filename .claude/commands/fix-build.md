---
description: Build and test the solution, then fix failures until everything passes
---
Run `dotnet build` from the repo root. Fix every error (warnings are errors here). Then run `dotnet test` and fix every failing test.

Rules:
- Fix the cause in the production code unless the test is clearly wrong. If you change a test's expectation, say why.
- Don't suppress warnings, add `#pragma`, or add `!` to silence nullable warnings without a comment saying why.
- Repeat build → test until both are clean, at most 5 rounds. If still failing, stop and explain what's left.

Finish with a short summary: what was broken, what you changed, and the final test count.
$ARGUMENTS
