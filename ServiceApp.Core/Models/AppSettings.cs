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

    /// <summary>
    /// Pfad zur Excel-Datei mit den Servicetickets für das Ticket-Dashboard. Leer, wenn
    /// noch nicht konfiguriert.
    /// </summary>
    public string TicketExcelFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Im Ticket-Dashboard ausgewählte Werte des Typ-Filters (Mehrfachauswahl,
    /// Excel-Spalte "Typ"). Eine leere Liste bedeutet "kein Filter, alle Typen
    /// anzeigen" - sowohl beim allerersten Start (noch nie konfiguriert) als auch,
    /// wenn der Benutzer bewusst alle Typen ausgewählt hat.
    /// </summary>
    public List<string> TicketSelectedTypes { get; set; } = new();

    /// <summary>
    /// Anzahl der Störungsorte (Excel-Spalte "Fehlercode Ort"), die im Kuchendiagramm
    /// des Ticket-Dashboards einzeln angezeigt werden; alle übrigen werden zu
    /// "Sonstige" zusammengefasst.
    /// </summary>
    public int TicketErrorLocationTopCount { get; set; } = 8;
}
