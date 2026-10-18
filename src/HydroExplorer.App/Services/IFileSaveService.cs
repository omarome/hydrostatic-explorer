namespace HydroExplorer.App.Services;

/// <summary>
/// Asks the user where to save a text file, then writes it. An interface so ViewModels can save files
/// without referencing windows or dialogs, and tests can use a fake instead of a real file dialog.
/// </summary>
public interface IFileSaveService
{
    /// <summary>
    /// Shows a save dialog suggesting <paramref name="suggestedFileName"/> and writes <paramref name="content"/>
    /// to the chosen file.
    /// </summary>
    /// <returns>The saved file's name, or null if the user cancelled.</returns>
    /// <exception cref="IOException">The file could not be written.</exception>
    /// <exception cref="UnauthorizedAccessException">No permission to write the file.</exception>
    Task<string?> SaveTextAsync(string suggestedFileName, string content);
}
