namespace ServiceApp.Core.Models;

/// <summary>
/// Repräsentiert einen einzelnen Anrufdatensatz, wie er aus einem Telefonbericht-PDF
/// extrahiert wurde. Ein <see cref="CallRecord"/> ist die kleinste Dateneinheit, aus der
/// alle Kennzahlen im Dashboard berechnet werden.
/// </summary>
/// <param name="ServiceNumberId">
/// Interne Kennung der Servicenummer, zu der dieser Anruf gehört (verweist auf
/// <see cref="ServiceNumberSettings.Id"/>).
/// </param>
/// <param name="Timestamp">Zeitpunkt (Datum + Uhrzeit) des Anrufs.</param>
/// <param name="Status">Ob der Anruf angenommen oder verpasst wurde.</param>
/// <param name="DurationSeconds">
/// Gesprächsdauer in Sekunden, sofern im Bericht vorhanden. <c>null</c>, wenn das
/// PDF-Format diese Information nicht liefert (z. B. bei verpassten Anrufen).
/// </param>
/// <param name="SourceFile">
/// Dateiname des PDFs, aus dem dieser Datensatz stammt. Dient der Nachvollziehbarkeit
/// und Fehlersuche (welches Dokument lieferte welche Daten).
/// </param>
public sealed record CallRecord(
    string ServiceNumberId,
    DateTime Timestamp,
    CallStatus Status,
    int? DurationSeconds,
    string SourceFile)
{
    /// <summary>Kurzform für die häufig benötigte Prüfung "wurde angenommen".</summary>
    public bool IsAnswered => Status == CallStatus.Answered;
}

/// <summary>Ergebnis eines Anrufs, wie es im Telefonbericht ausgewiesen wird.</summary>
public enum CallStatus
{
    /// <summary>Der Anruf wurde von einem Mitarbeiter angenommen.</summary>
    Answered,

    /// <summary>Der Anruf wurde nicht angenommen (verpasst, aufgelegt, Warteschlange verlassen).</summary>
    Missed
}
