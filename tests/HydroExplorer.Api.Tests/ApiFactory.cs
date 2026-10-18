using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HydroExplorer.Api.Tests;

/// <summary>
/// Starts the real API in memory (no network port) for integration tests.
/// WebApplicationFactory runs Program.cs in the Development environment; this subclass
/// only overrides the simulated latency so tests run fast.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public int LatencyMilliseconds { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Overrides "SimulatedLatency:Milliseconds" from appsettings.json (":" separates nested keys).
        builder.UseSetting("SimulatedLatency:Milliseconds", LatencyMilliseconds.ToString());
    }
}
