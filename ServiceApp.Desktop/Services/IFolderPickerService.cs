namespace ServiceApp.Desktop.Services;

/// <summary>
/// Abstrahiert die Auswahl eines Ordners über einen nativen Dateidialog. Durch dieses
/// Interface bleiben ViewModels (siehe <see cref="ViewModels.ServiceNumberPanelViewModel"/>)
/// frei von direkten Abhängigkeiten zu Avalonia-Fenstern und damit weiterhin ohne
/// laufende Oberfläche testbar.
/// </summary>
public interface IFolderPickerService
{
    /// <summary>
    /// Zeigt einen Ordnerauswahl-Dialog. Gibt den gewählten Pfad zurück, oder
    /// <c>null</c>, wenn der Benutzer den Dialog abgebrochen hat.
    /// </summary>
    Task<string?> PickFolderAsync(string title);
}
