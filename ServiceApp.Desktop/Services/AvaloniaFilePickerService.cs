using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace ServiceApp.Desktop.Services;

/// <summary>
/// Implementiert <see cref="IFilePickerService"/> über Avalonias plattformübergreifenden
/// <see cref="IStorageProvider"/>-Dialog, angebunden an das aktuelle Hauptfenster der
/// Anwendung.
/// </summary>
public sealed class AvaloniaFilePickerService : IFilePickerService
{
    public async Task<string?> PickFileAsync(string title, params string[] extensions)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow is null)
        {
            return null;
        }

        var storageProvider = desktop.MainWindow.StorageProvider;
        var patterns = extensions.Select(ext => $"*.{ext}").ToArray();

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType(title) { Patterns = patterns }
            }
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }
}
