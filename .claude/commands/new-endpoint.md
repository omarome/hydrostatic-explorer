---
description: Add a new API endpoint end to end (API, client, tests, README)
argument-hint: <what the endpoint should return>
---
Add a new endpoint to HydroExplorer.Api: $ARGUMENTS

Before writing code, look at `src/HydroExplorer.Api/Endpoints/HullEndpoints.cs` and `Queries/HydrostaticsQuery.cs`, then propose the route, query parameters, validation rules and response type, and wait for my OK.

Then:
1. Put any calculation in `HydroExplorer.Core` (no HTTP there), with tests in `HydroExplorer.Core.Tests`.
2. Add the endpoint in `Endpoints/`. Invalid parameters → 400 ProblemDetails naming the parameter; unknown hull → 404.
3. Add integration tests in `tests/HydroExplorer.Api.Tests` using `ApiFactory` (happy path, 400, 404).
4. If the app needs it: add a method to `IHydroApiClient` / `HydroApiClient` (InvariantCulture in URLs), update `FakeHydroApiClient`, and test with the stub handler in `HydroApiClientTests`.
5. Add the endpoint to the API reference table and a curl example in README.md.
6. Run `dotnet build` and `dotnet test`. Both must pass.
