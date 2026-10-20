namespace ServiceApp.Core.Models;

/// <summary>
/// Repräsentiert ein einzelnes Serviceticket, wie es aus der Excel-Tabelle importiert
/// wird. Enthält bewusst nur die für die Auswertung benötigten Felder (Erstellungsdatum
/// für den zeitlichen Verlauf, Fehlerursache für das Kuchendiagramm, Typ für den
/// Mehrfachauswahl-Filter) statt aller in der Beispieldatei vorhandenen Spalten.
/// </summary>
/// <param name="TicketNumber">Ticketnummer, dient der Nachvollziehbarkeit.</param>
/// <param name="CreatedAt">Anlagedatum des Tickets.</param>
/// <param name="Cause">
/// Inhalt der Spalte "Fehlercode Ursache". <c>null</c>, wenn die Zelle leer war.
/// </param>
/// <param name="Type">
/// Inhalt der Spalte "Typ" (z. B. "Störung", "Anforderung Mechaniker"). <c>null</c>,
/// wenn die Zelle leer war oder die Spalte in der Datei nicht vorhanden ist.
/// </param>
/// <param name="AddressLine">Inhalt der Spalte "Adresszeile 1".</param>
/// <param name="ErrorLocation">Inhalt der Spalte "Fehlercode Ort".</param>
/// <param name="ErrorFix">Inhalt der Spalte "Fehlercode Behebung".</param>
/// <param name="InternalStatus">Inhalt der Spalte "Interner Status".</param>
/// <param name="Responsible">Inhalt der Spalte "Verantwortlich".</param>
/// <param name="ExternalStatus">
/// Inhalt der Spalte "Externer Status" (z. B. "Abgeschlossen", "In Bearbeitung").
/// Bestimmt für die Bearbeitszeit-Auswertung, welche Tickets als abgeschlossen
/// gelten (siehe <see cref="Statistics.ITicketStatisticsService"/>).
/// </param>
/// <param name="LastChangedAt">
/// Inhalt der Spalte "Letzte Änderung". <c>null</c>, wenn die Zelle leer war oder
/// die Spalte in der Datei nicht vorhanden ist. Zusammen mit <see cref="CreatedAt"/>
/// Grundlage der Bearbeitszeit-Berechnung (Letzte Änderung − Anlagedatum).
/// </param>
public sealed record ServiceTicket(
    int TicketNumber,
    DateTime CreatedAt,
    string? Cause,
    string? Type,
    string? AddressLine = null,
    string? ErrorLocation = null,
    string? ErrorFix = null,
    string? InternalStatus = null,
    string? Responsible = null,
    string? ExternalStatus = null,
    DateTime? LastChangedAt = null);
