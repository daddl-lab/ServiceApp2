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

    /// <summary>
    /// UNC-Pfad des Zeichnungsarchiv-Netzlaufwerks (z. B. <c>\\SERVER\Zeichnungsarchiv</c>).
    /// Leer, wenn noch nicht konfiguriert.
    /// </summary>
    public string DrawingArchivePath { get; set; } = string.Empty;

    /// <summary>
    /// Speicherort der lokalen SQLite-Indexdatenbank für die Archivsuche. Leer bedeutet:
    /// Standardpfad unter <c>%AppData%/ServiceApp/index</c> verwenden (siehe
    /// <see cref="FileArchive.FileArchiveIndexStore"/>).
    /// </summary>
    public string FileArchiveIndexDatabasePath { get; set; } = string.Empty;

    /// <summary>Ob der Suchindex automatisch im Hintergrund aktuell gehalten werden soll.</summary>
    public bool FileArchiveAutoUpdateEnabled { get; set; } = true;

    /// <summary>Intervall zwischen automatischen inkrementellen Index-Aktualisierungen, in Minuten.</summary>
    public int FileArchiveUpdateIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// Obergrenze der in der Trefferliste angezeigten (und tatsächlich gesuchten) Dateien,
    /// um Oberfläche und Speicherverbrauch bei sehr unspezifischen Suchbegriffen zu schützen.
    /// </summary>
    public int FileArchiveMaxDisplayedResults { get; set; } = 5000;

    /// <summary>Ob die Archivsuche standardmäßig Groß-/Kleinschreibung berücksichtigt (Standard: nein).</summary>
    public bool FileArchiveCaseSensitiveSearch { get; set; }

    /// <summary>
    /// Zeichenlängen der ersten drei Verzeichnisebenen des Archivs, in dieser Reihenfolge.
    /// Standard 2/3/4 Zeichen entspricht dem in der Aufgabenstellung beschriebenen Beispiel
    /// (Suchbegriff "123456789" → Pfad "12\345\6789"); konfigurierbar, falls die reale
    /// Ordnerstruktur des Archivs davon abweicht.
    /// </summary>
    public int[] FileArchiveLevelLengths { get; set; } = { 2, 3, 4 };
}
