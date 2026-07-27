namespace ServiceApp.Core.Models;

/// <summary>
/// Wurzelobjekt der persistierten Anwendungseinstellungen. Wird 1:1 nach
/// <c>settings.json</c> serialisiert und beim Programmstart automatisch geladen
/// (siehe <see cref="Configuration.ISettingsService"/>).
/// </summary>
public sealed class AppSettings
{
    /// <summary>
    /// Konfigurierte Servicenummern. Die Anwendung ist aktuell auf zwei Nummern
    /// ausgelegt, die Liste selbst ist aber nicht auf zwei Einträge begrenzt, damit
    /// künftige Erweiterungen (weitere Servicenummern) ohne Strukturänderung möglich sind.
    /// </summary>
    public List<ServiceNumberSettings> ServiceNumbers { get; set; } = new()
    {
        new ServiceNumberSettings { Id = "service-1", Name = "Servicenummer 1", PdfFolderPath = string.Empty },
        new ServiceNumberSettings { Id = "service-2", Name = "Servicenummer 2", PdfFolderPath = string.Empty }
    };
}
