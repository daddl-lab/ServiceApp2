using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ServiceApp.Core.Configuration;
using ServiceApp.Core.Models;
using ServiceApp.Desktop.Services;

namespace ServiceApp.Desktop.ViewModels;

/// <summary>
/// ViewModel der Einstellungen-Ansicht. Lädt beim Erstellen die zuletzt gespeicherten
/// Einstellungen (automatisches Laden beim Programmstart) und persistiert Änderungen
/// über <see cref="ISettingsService"/> dauerhaft als JSON-Datei.
/// </summary>
public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IFilePickerService _filePicker;
    private readonly ILogger<SettingsViewModel> _logger;

    public ServiceNumberPanelViewModel FirstServiceNumber { get; }

    public ServiceNumberPanelViewModel SecondServiceNumber { get; }

    [ObservableProperty]
    private string _ticketExcelFilePath;

    [ObservableProperty]
    private string? _ticketExcelValidationMessage;

    [ObservableProperty]
    private int _ticketErrorLocationTopCount;

    [ObservableProperty]
    private string? _ticketErrorLocationTopCountValidationMessage;

    [ObservableProperty]
    private string? _statusMessage;

    /// <summary>
    /// Wird ausgelöst, nachdem Einstellungen erfolgreich gespeichert wurden, damit die
    /// Dashboards ihre Daten mit den (möglicherweise geänderten) Pfaden neu laden können.
    /// </summary>
    public event EventHandler? SettingsSaved;

    public SettingsViewModel(
        ISettingsService settingsService,
        IFolderPickerService folderPicker,
        IFilePickerService filePicker,
        ILogger<SettingsViewModel> logger)
    {
        _settingsService = settingsService;
        _filePicker = filePicker;
        _logger = logger;

        var settings = _settingsService.Load();
        FirstServiceNumber = new ServiceNumberPanelViewModel(settings.ServiceNumbers[0], folderPicker);
        SecondServiceNumber = new ServiceNumberPanelViewModel(settings.ServiceNumbers[1], folderPicker);
        _ticketExcelFilePath = settings.TicketExcelFilePath;
        _ticketErrorLocationTopCount = settings.TicketErrorLocationTopCount;
    }

    [RelayCommand]
    private async Task BrowseTicketExcelFileAsync()
    {
        var selected = await _filePicker.PickFileAsync("Excel-Datei mit Servicetickets wählen", "xlsx");
        if (selected is not null)
        {
            TicketExcelFilePath = selected;
        }
    }

    /// <summary>
    /// Prüft, ob der eingegebene Dateipfad gültig ist. Ein leerer Pfad gilt als gültig
    /// (Ticket-Dashboard noch nicht konfiguriert), ein nicht-leerer Pfad muss auf eine
    /// tatsächlich existierende Datei verweisen.
    /// </summary>
    private bool ValidateTicketExcelPath()
    {
        if (string.IsNullOrWhiteSpace(TicketExcelFilePath))
        {
            TicketExcelValidationMessage = null;
            return true;
        }

        if (!File.Exists(TicketExcelFilePath))
        {
            TicketExcelValidationMessage = $"Die Datei \"{TicketExcelFilePath}\" existiert nicht.";
            return false;
        }

        TicketExcelValidationMessage = null;
        return true;
    }

    /// <summary>Prüft, ob die Top-X-Anzahl für das Störungsort-Kuchendiagramm ein gültiger Wert (mindestens 1) ist.</summary>
    private bool ValidateTicketErrorLocationTopCount()
    {
        if (TicketErrorLocationTopCount < 1)
        {
            TicketErrorLocationTopCountValidationMessage = "Bitte geben Sie eine Zahl von mindestens 1 ein.";
            return false;
        }

        TicketErrorLocationTopCountValidationMessage = null;
        return true;
    }

    [RelayCommand]
    private void Save()
    {
        var firstValid = FirstServiceNumber.Validate();
        var secondValid = SecondServiceNumber.Validate();
        var ticketPathValid = ValidateTicketExcelPath();
        var topCountValid = ValidateTicketErrorLocationTopCount();

        if (!firstValid || !secondValid || !ticketPathValid || !topCountValid)
        {
            StatusMessage = "Bitte prüfen Sie die markierten Felder, bevor Sie speichern.";
            return;
        }

        // Aktuelle Einstellungen laden statt eine neue AppSettings-Instanz zu bauen, damit
        // Felder, die nicht auf dieser Ansicht bearbeitet werden (z. B. der
        // Typ-Filter des Ticket-Dashboards), beim Speichern nicht überschrieben werden.
        var settings = _settingsService.Load();
        settings.ServiceNumbers = new List<ServiceNumberSettings>
        {
            FirstServiceNumber.ToSettings(),
            SecondServiceNumber.ToSettings()
        };
        settings.TicketExcelFilePath = TicketExcelFilePath;
        settings.TicketErrorLocationTopCount = TicketErrorLocationTopCount;

        _settingsService.Save(settings);
        _logger.LogInformation("Einstellungen wurden über die Oberfläche geändert und gespeichert.");
        StatusMessage = "Einstellungen wurden gespeichert.";

        SettingsSaved?.Invoke(this, EventArgs.Empty);
    }
}
