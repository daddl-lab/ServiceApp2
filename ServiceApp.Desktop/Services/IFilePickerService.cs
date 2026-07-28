namespace ServiceApp.Desktop.Services;

/// <summary>
/// Abstrahiert die Auswahl einer einzelnen Datei über einen nativen Dateidialog.
/// Ergänzt <see cref="IFolderPickerService"/> (Ordnerauswahl) um die Auswahl einer
/// konkreten Datei, z. B. der Serviceticket-Exceldatei.
/// </summary>
public interface IFilePickerService
{
    /// <summary>
    /// Zeigt einen Dateiauswahl-Dialog, gefiltert auf die angegebenen Endungen (ohne
    /// Punkt, z. B. <c>"xlsx"</c>). Gibt den gewählten Pfad zurück, oder <c>null</c>,
    /// wenn der Benutzer den Dialog abgebrochen hat.
    /// </summary>
    Task<string?> PickFileAsync(string title, params string[] extensions);
}
