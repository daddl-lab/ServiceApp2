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

    [RelayCommand]
    private void Save()
    {
        var firstValid = FirstServiceNumber.Validate();
        var secondValid = SecondServiceNumber.Validate();
        var ticketPathValid = ValidateTicketExcelPath();

        if (!firstValid || !secondValid || !ticketPathValid)
        {
            StatusMessage = "Bitte prüfen Sie die markierten Pfade, bevor Sie speichern.";
            return;
        }

        var settings = new AppSettings
        {
            ServiceNumbers = new List<ServiceNumberSettings>
            {
                FirstServiceNumber.ToSettings(),
                SecondServiceNumber.ToSettings()
            },
            TicketExcelFilePath = TicketExcelFilePath
        };

        _settingsService.Save(settings);
        _logger.LogInformation("Einstellungen wurden über die Oberfläche geändert und gespeichert.");
        StatusMessage = "Einstellungen wurden gespeichert.";

        SettingsSaved?.Invoke(this, EventArgs.Empty);
    }
}
