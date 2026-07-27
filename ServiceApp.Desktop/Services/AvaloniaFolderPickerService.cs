using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace ServiceApp.Desktop.Services;

/// <summary>
/// Implementiert <see cref="IFolderPickerService"/> über Avalonias plattformübergreifenden
/// <see cref="IStorageProvider"/>-Dialog, angebunden an das aktuelle Hauptfenster der
/// Anwendung.
/// </summary>
public sealed class AvaloniaFolderPickerService : IFolderPickerService
{
    public async Task<string?> PickFolderAsync(string title)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow is null)
        {
            return null;
        }

        var storageProvider = desktop.MainWindow.StorageProvider;
        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }
}
