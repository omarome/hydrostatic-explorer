using HydroExplorer.App.Services;

namespace HydroExplorer.App.Tests;

/// <summary>Stand-in for the save dialog: records what would be saved, or simulates Cancel or a write error.</summary>
public sealed class FakeFileSaveService : IFileSaveService
{
    /// <summary>When true, behaves as if the user pressed Cancel in the dialog.</summary>
    public bool Cancel { get; set; }

    /// <summary>When set, SaveTextAsync throws it, like a full disk or a read-only folder.</summary>
    public Exception? Failure { get; set; }

    public string? SuggestedFileName { get; private set; }

    public string? SavedContent { get; private set; }

    public Task<string?> SaveTextAsync(string suggestedFileName, string content)
    {
        SuggestedFileName = suggestedFileName;
        if (Failure is not null)
        {
            throw Failure;
        }

        if (Cancel)
        {
            return Task.FromResult<string?>(null);
        }

        SavedContent = content;
        return Task.FromResult<string?>(suggestedFileName);
    }
}
