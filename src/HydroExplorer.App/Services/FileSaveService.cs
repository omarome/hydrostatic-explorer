using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace HydroExplorer.App.Services;

/// <summary>
/// <see cref="IFileSaveService"/> using the operating system's save dialog (on macOS, the native sheet).
/// </summary>
/// <remarks>
/// WPF note: WPF uses Microsoft.Win32.SaveFileDialog with ShowDialog(). Avalonia's dialogs are async and
/// belong to a window's StorageProvider, so the same code works on macOS, Windows and Linux.
/// </remarks>
public sealed class FileSaveService : IFileSaveService
{
    private static readonly FilePickerFileType CsvFileType = new("CSV file")
    {
        Patterns = new[] { "*.csv" },
        AppleUniformTypeIdentifiers = new[] { "public.comma-separated-values-text" },
        MimeTypes = new[] { "text/csv" },
    };

    public async Task<string?> SaveTextAsync(string suggestedFileName, string content)
    {
        // The dialog is attached to the main window. Looked up at call time, because this service
        // is created (by DI) before the window exists.
        Window? window = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (window is null)
        {
            return null;
        }

        IStorageFile? file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export page as CSV",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "csv",
            FileTypeChoices = new[] { CsvFileType },
            ShowOverwritePrompt = true,
        });
        if (file is null)
        {
            return null;  // the user pressed Cancel
        }

        // "await using" disposes (closes) the stream and writer when the block ends, even after an
        // exception, like try/finally. UTF-8 without a byte-order mark is the most portable choice.
        await using Stream stream = await file.OpenWriteAsync();
        if (stream.CanSeek)
        {
            stream.SetLength(0);  // replacing an existing file: drop any old content longer than the new
        }

        await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        await writer.WriteAsync(content);
        return file.Name;
    }
}
