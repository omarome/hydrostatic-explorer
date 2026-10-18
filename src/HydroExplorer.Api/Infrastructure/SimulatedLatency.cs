using Microsoft.Extensions.Options;

namespace HydroExplorer.Api.Infrastructure;

/// <summary>
/// Settings for the artificial delay, bound from the "SimulatedLatency" section of appsettings.json.
/// This is the .NET "options pattern": a plain class filled from configuration.
/// </summary>
public sealed class SimulatedLatencyOptions
{
    public const string SectionName = "SimulatedLatency";

    /// <summary>Delay added to every /api request, in milliseconds. 0 turns it off.</summary>
    public int Milliseconds { get; set; } = 200;
}

/// <summary>
/// Middleware that waits before handling each /api request, so the desktop app's loading
/// indicators are visible on a fast local machine. Middleware here works like Express middleware.
/// </summary>
public sealed class SimulatedLatencyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IOptionsMonitor<SimulatedLatencyOptions> _options;

    public SimulatedLatencyMiddleware(RequestDelegate next, IOptionsMonitor<SimulatedLatencyOptions> options)
    {
        _next = next;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // IOptionsMonitor.CurrentValue picks up edits to appsettings.json without a restart.
        int delay = _options.CurrentValue.Milliseconds;
        if (delay > 0 && context.Request.Path.StartsWithSegments("/api"))
        {
            // RequestAborted cancels the wait if the client disconnects.
            await Task.Delay(delay, context.RequestAborted);
        }

        await _next(context);
    }
}
