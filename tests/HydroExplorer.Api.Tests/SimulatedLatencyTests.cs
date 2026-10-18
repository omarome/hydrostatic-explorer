using System.Diagnostics;

namespace HydroExplorer.Api.Tests;

public class SimulatedLatencyTests
{
    [Fact]
    public async Task ApiRequests_AreDelayedByConfiguredLatency()
    {
        // "using" disposes the factory (stops the in-memory server) at the end of the method.
        using var factory = new ApiFactory { LatencyMilliseconds = 300 };
        HttpClient client = factory.CreateClient();
        await client.GetAsync("/swagger/v1/swagger.json");  // warm up: the first request starts the server

        var stopwatch = Stopwatch.StartNew();
        await client.GetAsync("/api/hulls");
        stopwatch.Stop();

        // Only a lower bound: a slow machine may take longer, but never less than the delay.
        Assert.True(stopwatch.ElapsedMilliseconds >= 290, $"Took only {stopwatch.ElapsedMilliseconds} ms.");
    }
}
