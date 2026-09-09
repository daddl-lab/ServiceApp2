using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ServiceApp.Core.Configuration;
using ServiceApp.Core.Exceptions;
using ServiceApp.Core.FileArchive;
using ServiceApp.Core.Models;
using ServiceApp.Desktop.Services;

namespace ServiceApp.Desktop.ViewModels;

/// <summary>
/// ViewModel der Zeichnungsarchiv-Dateisuche. Streamt Treffer von
/// <see cref="IFileArchiveSearchService"/> fortlaufend in <see cref="Results"/>, damit die
/// Oberfläche erste Ergebnisse zeigt, sobald sie gefunden werden, statt auf den Abschluss
/// der gesamten Suche zu warten. Eine neu gestartete Suche bricht eine noch laufende
/// vorherige automatisch ab.
/// </summary>
public sealed partial class FileSearchViewModel : ViewModelBase
{
    private readonly IFileArchiveSearchService _searchService;
    private readonly ISettingsService _settingsService;
    private readonly IShellLaunchService _shellLaunchService;
    private readonly ILogger<FileSearchViewModel> _logger;
    private CancellationTokenSource? _searchCancellation;

    [ObservableProperty]
    private string _searchTerm = string.Empty;

    [ObservableProperty]
    private FileTypeFilter _selectedFileType = FileTypeFilter.All;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private int _foundCount;

    [ObservableProperty]
    private string? _elapsedTimeText;

    /// <summary>Zugrundeliegende Sammlung der Treffer. Wird von der Suche fortlaufend befüllt.</summary>
    public ObservableCollection<FileArchiveEntry> Results { get; } = new();

    /// <summary>An das <c>DataGrid</c> gebundene Sicht auf <see cref="Results"/>, die Spalten-Sortierung durch Klick auf die Kopfzeile ermöglicht.</summary>
    public DataGridCollectionView ResultsView { get; }

    /// <summary>Auswahlmöglichkeiten für den Dateityp-Filter, "Alle Dateitypen" zuerst.</summary>
    public IReadOnlyList<FileTypeFilter> FileTypeOptions { get; } = Enum.GetValues<FileTypeFilter>();

    public FileSearchViewModel(
        IFileArchiveSearchService searchService,
        ISettingsService settingsService,
        IShellLaunchService shellLaunchService,
        ILogger<FileSearchViewModel> logger)
    {
        _searchService = searchService;
        _settingsService = settingsService;
        _shellLaunchService = shellLaunchService;
        _logger = logger;
        ResultsView = new DataGridCollectionView(Results);
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        _searchCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _searchCancellation = cancellation;

        Results.Clear();
        FoundCount = 0;
        ElapsedTimeText = null;

        var term = SearchTerm;
        if (string.IsNullOrWhiteSpace(term))
        {
            StatusMessage = "Bitte geben Sie einen Suchbegriff ein.";
            return;
        }

        var settings = await Task.Run(_settingsService.Load, cancellation.Token).ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(settings.DrawingArchivePath))
        {
            StatusMessage = "Bitte konfigurieren Sie zunächst den Archivpfad in den Einstellungen.";
            return;
        }

        var levelLengths = settings.FileArchiveLevelLengths is { Length: 3 }
            ? settings.FileArchiveLevelLengths
            : ArchivePathResolver.DefaultLevelLengths.ToArray();
        var query = new ArchiveSearchQuery(term, SelectedFileType, settings.FileArchiveCaseSensitiveSearch, settings.FileArchiveMaxDisplayedResults);

        IsSearching = true;
        StatusMessage = "Suche läuft…";
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await foreach (var entry in _searchService.SearchAsync(query, settings.DrawingArchivePath, levelLengths, cancellation.Token))
            {
                Results.Add(entry);
                FoundCount++;
                if (FoundCount % 25 == 0)
                {
                    StatusMessage = $"Suche läuft… Gefundene Dateien: {FoundCount}";
                }
            }

            stopwatch.Stop();
            StatusMessage = FoundCount == 0
                ? "Suche abgeschlossen. Keine Dateien gefunden."
                : $"Suche abgeschlossen. {FoundCount} Dateien gefunden.";
            ElapsedTimeText = FormatElapsed(stopwatch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            // Eine neuere Suche hat diese ersetzt (oder die Ansicht wurde geschlossen) - kein Fehlerfall.
        }
        catch (InvalidSearchTermException ex)
        {
            StatusMessage = ex.Message;
        }
        catch (FileArchiveException ex)
        {
            _logger.LogError(ex, "Fehler bei der Archivsuche nach \"{Term}\".", term);
            StatusMessage = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unerwarteter Fehler bei der Archivsuche nach \"{Term}\".", term);
            StatusMessage = "Bei der Suche ist ein unerwarteter Fehler aufgetreten. Bitte versuchen Sie es erneut.";
        }
        finally
        {
            IsSearching = false;
        }
    }

    private static string FormatElapsed(TimeSpan elapsed)
        => string.Format(CultureInfo.GetCultureInfo("de-DE"), "{0:0.0} Sekunden", elapsed.TotalSeconds);

    [RelayCommand]
    private void OpenFile(FileArchiveEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        try
        {
            _shellLaunchService.OpenFile(entry.FullPath);
        }
        catch (ShellLaunchException ex)
        {
            _logger.LogWarning(ex, "Datei \"{File}\" konnte nicht geöffnet werden.", entry.FullPath);
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void OpenContainingFolder(FileArchiveEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        try
        {
            _shellLaunchService.OpenContainingFolder(entry.FullPath);
        }
        catch (ShellLaunchException ex)
        {
            _logger.LogWarning(ex, "Ordner für \"{File}\" konnte nicht geöffnet werden.", entry.FullPath);
            StatusMessage = ex.Message;
        }
    }
}
