using System.ComponentModel;
using HydroExplorer.App.Services;

namespace HydroExplorer.App.ViewModels;

/// <summary>Formats <see cref="ApiStatus"/> for the status bar at the bottom of the window.</summary>
public class StatusBarViewModel : ViewModelBase
{
    private readonly ApiStatus _status;

    public StatusBarViewModel(ApiStatus status, HttpClient http)
    {
        _status = status;
        ApiAddress = http.BaseAddress?.ToString() ?? "(no address)";
        _status.PropertyChanged += OnStatusChanged;  // app-lifetime singletons: no unsubscribe needed
    }

    public string ApiAddress { get; }

    public ApiConnectionState State => _status.State;

    public bool IsConnected => State == ApiConnectionState.Connected;

    public bool IsDisconnected => State == ApiConnectionState.Disconnected;

    /// <summary>For example "Connected to http://localhost:5080/".</summary>
    public string ConnectionText => _status.State switch
    {
        ApiConnectionState.Connected => $"Connected to {ApiAddress}",
        ApiConnectionState.Disconnected => $"API unavailable: {_status.LastError}",
        _ => $"Connecting to {ApiAddress}…",
    };

    /// <summary>For example "Last load 14:32:05 (212 ms)".</summary>
    public string LastLoadText => _status.LastSuccessTime is DateTime time
        ? $"Last load {time:HH:mm:ss} ({_status.LastDuration?.TotalMilliseconds:0} ms)"
        : "No data loaded yet";

    private void OnStatusChanged(object? sender, PropertyChangedEventArgs e)
    {
        // These properties are computed from ApiStatus, so when it changes, all of them may have changed.
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(IsDisconnected));
        OnPropertyChanged(nameof(ConnectionText));
        OnPropertyChanged(nameof(LastLoadText));
    }
}
