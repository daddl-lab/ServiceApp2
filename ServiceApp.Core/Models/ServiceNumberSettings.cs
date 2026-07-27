namespace ServiceApp.Core.Models;

/// <summary>
/// Konfiguration einer einzelnen Servicenummer: Anzeigename und der Ordner, in dem die
/// zugehörigen PDF-Telefonberichte abgelegt werden. Wird über die Einstellungen-Ansicht
/// bearbeitet und in <see cref="AppSettings"/> dauerhaft gespeichert.
/// </summary>
public sealed class ServiceNumberSettings
{
    /// <summary>
    /// Stabile, unveränderliche Kennung der Servicenummer (z. B. "service-1").
    /// Wird intern verwendet, um Anrufdaten eindeutig einer Nummer zuzuordnen,
    /// auch wenn der Anzeigename später geändert wird.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Anzeigename der Servicenummer, wie er im Dashboard erscheint (z. B. "Support-Hotline").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Absoluter Pfad zu dem Ordner, der die PDF-Berichte dieser Servicenummer enthält.</summary>
    public string PdfFolderPath { get; set; } = string.Empty;
}
