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
    private readonly ILogger<SettingsViewModel> _logger;

    public ServiceNumberPanelViewModel FirstServiceNumber { get; }

    public ServiceNumberPanelViewModel SecondServiceNumber { get; }

    [ObservableProperty]
    private string? _statusMessage;

    /// <summary>
    /// Wird ausgelöst, nachdem Einstellungen erfolgreich gespeichert wurden, damit das
    /// Dashboard seine Daten mit den (möglicherweise geänderten) Ordnerpfaden neu laden kann.
    /// </summary>
    public event EventHandler? SettingsSaved;

    public SettingsViewModel(ISettingsService settingsService, IFolderPickerService folderPicker, ILogger<SettingsViewModel> logger)
    {
        _settingsService = settingsService;
        _logger = logger;

        var settings = _settingsService.Load();
        FirstServiceNumber = new ServiceNumberPanelViewModel(settings.ServiceNumbers[0], folderPicker);
        SecondServiceNumber = new ServiceNumberPanelViewModel(settings.ServiceNumbers[1], folderPicker);
    }

    [RelayCommand]
    private void Save()
    {
        var firstValid = FirstServiceNumber.Validate();
        var secondValid = SecondServiceNumber.Validate();

        if (!firstValid || !secondValid)
        {
            StatusMessage = "Bitte prüfen Sie die markierten Ordnerpfade, bevor Sie speichern.";
            return;
        }

        var settings = new AppSettings
        {
            ServiceNumbers = new List<ServiceNumberSettings>
            {
                FirstServiceNumber.ToSettings(),
                SecondServiceNumber.ToSettings()
            }
        };

        _settingsService.Save(settings);
        _logger.LogInformation("Einstellungen wurden über die Oberfläche geändert und gespeichert.");
        StatusMessage = "Einstellungen wurden gespeichert.";

        SettingsSaved?.Invoke(this, EventArgs.Empty);
    }
}
