namespace ServiceApp.Core.Models;

/// <summary>
/// Repräsentiert ein einzelnes Serviceticket, wie es aus der Excel-Tabelle importiert
/// wird. Enthält bewusst nur die für die Auswertung benötigten Felder (Erstellungsdatum
/// für den zeitlichen Verlauf, Fehlerursache für das Kuchendiagramm) statt aller in der
/// Beispieldatei vorhandenen Spalten.
/// </summary>
/// <param name="TicketNumber">Ticketnummer, dient der Nachvollziehbarkeit.</param>
/// <param name="CreatedAt">Anlagedatum des Tickets.</param>
/// <param name="Cause">
/// Inhalt der Spalte "Fehlercode Ursache". <c>null</c>, wenn die Zelle leer war.
/// </param>
public sealed record ServiceTicket(int TicketNumber, DateTime CreatedAt, string? Cause);
