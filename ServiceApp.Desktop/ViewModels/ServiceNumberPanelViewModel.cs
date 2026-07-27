using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ServiceApp.Core.Models;
using ServiceApp.Desktop.Services;

namespace ServiceApp.Desktop.ViewModels;

/// <summary>
/// ViewModel für die Einstellungen einer einzelnen Servicenummer: Anzeigename und
/// PDF-Ordnerpfad. Zwei Instanzen dieses ViewModels bilden zusammen die
/// Einstellungen-Ansicht (siehe <see cref="SettingsViewModel"/>).
/// </summary>
public sealed partial class ServiceNumberPanelViewModel : ViewModelBase
{
    private readonly IFolderPickerService _folderPicker;

    /// <summary>Stabile Kennung der Servicenummer, wird beim Speichern unverändert übernommen.</summary>
    public string Id { get; }

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _pdfFolderPath;

    [ObservableProperty]
    private string? _validationMessage;

    public ServiceNumberPanelViewModel(ServiceNumberSettings settings, IFolderPickerService folderPicker)
    {
        _folderPicker = folderPicker;
        Id = settings.Id;
        _name = settings.Name;
        _pdfFolderPath = settings.PdfFolderPath;
    }

    [RelayCommand]
    private async Task BrowseFolderAsync()
    {
        var selected = await _folderPicker.PickFolderAsync($"PDF-Ordner für \"{Name}\" wählen");
        if (selected is not null)
        {
            PdfFolderPath = selected;
        }
    }

    /// <summary>
    /// Prüft, ob der eingegebene Ordnerpfad gültig ist. Ein leerer Pfad gilt als
    /// gültig (noch nicht konfigurierte Servicenummer), ein nicht-leerer Pfad muss auf
    /// einen tatsächlich existierenden Ordner verweisen.
    /// </summary>
    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(PdfFolderPath))
        {
            ValidationMessage = null;
            return true;
        }

        if (!Directory.Exists(PdfFolderPath))
        {
            ValidationMessage = $"Der Ordner \"{PdfFolderPath}\" existiert nicht.";
            return false;
        }

        ValidationMessage = null;
        return true;
    }

    public ServiceNumberSettings ToSettings() => new()
    {
        Id = Id,
        Name = Name,
        PdfFolderPath = PdfFolderPath
    };
}
