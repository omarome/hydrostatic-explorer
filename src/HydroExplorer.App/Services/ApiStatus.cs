using CommunityToolkit.Mvvm.ComponentModel;

namespace HydroExplorer.App.Services;

public enum ApiConnectionState
{
    /// <summary>No request has finished yet.</summary>
    Unknown,
    Connected,
    Disconnected,
}

/// <summary>
/// The outcome of the most recent API request, shown in the status bar. <see cref="HydroApiClient"/>
/// reports into it after every request, so the status bar needs no knowledge of which view loaded what.
/// </summary>
public partial class ApiStatus : ObservableObject
{
    [ObservableProperty]
    private ApiConnectionState _state = ApiConnectionState.Unknown;

    /// <summary>Local time the last successful request finished.</summary>
    [ObservableProperty]
    private DateTime? _lastSuccessTime;

    /// <summary>How long the last successful request took, including the API's simulated latency.</summary>
    [ObservableProperty]
    private TimeSpan? _lastDuration;

    [ObservableProperty]
    private string? _lastError;

    public void ReportSuccess(TimeSpan duration)
    {
        LastSuccessTime = DateTime.Now;
        LastDuration = duration;
        LastError = null;
        State = ApiConnectionState.Connected;
    }

    public void ReportFailure(string message)
    {
        LastError = message;
        State = ApiConnectionState.Disconnected;
    }
}
