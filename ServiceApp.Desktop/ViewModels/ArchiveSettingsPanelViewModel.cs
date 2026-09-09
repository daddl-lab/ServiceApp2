using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ServiceApp.Core.FileArchive;
using ServiceApp.Core.Models;
using ServiceApp.Desktop.Services;

namespace ServiceApp.Desktop.ViewModels;

/// <summary>
/// ViewModel für die Archiv-Einstellungen der Zeichnungsarchiv-Dateisuche: Netzlaufwerkpfad,
/// Index-Speicherort und -Verhalten sowie eine manuelle Index-Aktualisierung mit
/// Statusanzeige. Folgt demselben Aufbau wie <see cref="ServiceNumberPanelViewModel"/>, mit
/// einer bewussten Abweichung bei der Validierung: der Archivpfad wird NICHT auf
/// <see cref="Directory.Exists(string)"/> geprüft, da das Netzlaufwerk beim Bearbeiten der
/// Einstellungen durchaus vorübergehend nicht erreichbar sein darf, ohne dass das Speichern
/// der Konfiguration daran scheitern soll.
/// </summary>
public sealed partial class ArchiveSettingsPanelViewModel : ViewModelBase
{
    private readonly IFolderPickerService _folderPicker;
    private readonly IFileArchiveIndexService _indexService;
    private readonly ILogger<ArchiveSettingsPanelViewModel> _logger;

    [ObservableProperty]
    private string _drawingArchivePath;

    [ObservableProperty]
    private string _indexDatabasePath;

    [ObservableProperty]
    private bool _autoUpdateEnabled;

    [ObservableProperty]
    private int _updateIntervalMinutes;

    [ObservableProperty]
    private int _maxDisplayedResults;

    [ObservableProperty]
    private bool _caseSensitiveSearch;

    [ObservableProperty]
    private string? _validationMessage;

    [ObservableProperty]
    private bool _isUpdatingIndex;

    [ObservableProperty]
    private string _indexStatusText = "Status wird geladen…";

    public ArchiveSettingsPanelViewModel(
        AppSettings settings, IFolderPickerService folderPicker, IFileArchiveIndexService indexService, ILogger<ArchiveSettingsPanelViewModel> logger)
    {
        _folderPicker = folderPicker;
        _indexService = indexService;
        _logger = logger;

        _drawingArchivePath = settings.DrawingArchivePath;
        _indexDatabasePath = settings.FileArchiveIndexDatabasePath;
        _autoUpdateEnabled = settings.FileArchiveAutoUpdateEnabled;
        _updateIntervalMinutes = settings.FileArchiveUpdateIntervalMinutes;
        _maxDisplayedResults = settings.FileArchiveMaxDisplayedResults;
        _caseSensitiveSearch = settings.FileArchiveCaseSensitiveSearch;

        RefreshIndexStatus();
    }

    [RelayCommand]
    private async Task BrowseArchivePathAsync()
    {
        var selected = await _folderPicker.PickFolderAsync("Zeichnungsarchiv-Ordner wählen");
        if (selected is not null)
        {
            DrawingArchivePath = selected;
        }
    }

    [RelayCommand]
    private async Task BrowseIndexLocationAsync()
    {
        var selected = await _folderPicker.PickFolderAsync("Speicherort für den Suchindex wählen");
        if (selected is not null)
        {
            IndexDatabasePath = Path.Combine(selected, "filearchive-index.db");
        }
    }

    /// <summary>
    /// Prüft die eingegebenen Werte. Ein leerer Archivpfad ist gültig (Suche noch nicht
    /// konfiguriert); ein nicht-leerer Pfad wird bewusst nicht auf Erreichbarkeit geprüft
    /// (siehe Klassenkommentar).
    /// </summary>
    public bool Validate()
    {
        if (UpdateIntervalMinutes < 1)
        {
            ValidationMessage = "Das Aktualisierungsintervall muss mindestens 1 Minute betragen.";
            return false;
        }

        if (MaxDisplayedResults < 1)
        {
            ValidationMessage = "Die maximale Trefferzahl muss mindestens 1 betragen.";
            return false;
        }

        ValidationMessage = null;
        return true;
    }

    public void ApplyTo(AppSettings settings)
    {
        settings.DrawingArchivePath = DrawingArchivePath;
        settings.FileArchiveIndexDatabasePath = IndexDatabasePath;
        settings.FileArchiveAutoUpdateEnabled = AutoUpdateEnabled;
        settings.FileArchiveUpdateIntervalMinutes = UpdateIntervalMinutes;
        settings.FileArchiveMaxDisplayedResults = MaxDisplayedResults;
        settings.FileArchiveCaseSensitiveSearch = CaseSensitiveSearch;
    }

    [RelayCommand]
    private async Task RunIndexUpdateNowAsync()
    {
        if (string.IsNullOrWhiteSpace(DrawingArchivePath))
        {
            IndexStatusText = "Bitte zuerst einen Archivpfad angeben und speichern.";
            return;
        }

        IsUpdatingIndex = true;
        var progress = new Progress<ArchiveScanStatus>(s => IndexStatusText = $"Index wird aktualisiert… {s.ItemsProcessed} Ordner durchsucht ({s.CurrentArea}).");

        try
        {
            var wasReady = _indexService.GetStatus().IndexReady;
            if (wasReady)
            {
                await _indexService.RunIncrementalScanAsync(DrawingArchivePath, progress, CancellationToken.None);
            }
            else
            {
                await _indexService.RunFullScanAsync(DrawingArchivePath, progress, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manuelle Indexaktualisierung ist fehlgeschlagen.");
            IndexStatusText = $"Aktualisierung fehlgeschlagen: {ex.Message}";
            IsUpdatingIndex = false;
            return;
        }

        IsUpdatingIndex = false;
        RefreshIndexStatus();
    }

    private void RefreshIndexStatus()
    {
        var status = _indexService.GetStatus();
        IndexStatusText = status.IndexReady
            ? $"Index bereit: {status.TotalFileCount} Dateien, letzter Vollscan {status.LastFullScanCompletedUtc:dd.MM.yyyy HH:mm}, {status.LastScanErrorCount} übersprungene Elemente."
            : "Index wird noch aufgebaut, erste Suchen nutzen bis dahin einen eingegrenzten Direktzugriff.";
    }
}
