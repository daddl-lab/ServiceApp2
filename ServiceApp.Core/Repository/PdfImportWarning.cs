namespace ServiceApp.Core.Repository;

/// <summary>
/// Beschreibt eine einzelne PDF-Datei, die beim Einlesen eines Ordners übersprungen
/// wurde (z. B. weil sie beschädigt war oder ein unbekanntes Format hatte). Damit kann
/// die Oberfläche verständlich anzeigen, welche Dateien nicht ausgewertet werden
/// konnten, ohne dass ein einzelner Fehler den gesamten Import abbricht.
/// </summary>
/// <param name="FileName">Name der betroffenen Datei.</param>
/// <param name="Message">Verständliche, für die Anzeige geeignete Fehlermeldung.</param>
public sealed record PdfImportWarning(string FileName, string Message);

/// <summary>
/// Ergebnis des Einlesens aller PDF-Dateien einer Servicenummer: die erfolgreich
/// erkannten Anrufdatensätze sowie Warnungen zu Dateien, die übersprungen werden mussten.
/// </summary>
/// <param name="Records">Alle erfolgreich aus den PDFs extrahierten Anrufdatensätze.</param>
/// <param name="Warnings">Dateien, die aufgrund eines Fehlers nicht eingelesen werden konnten.</param>
public sealed record CallRecordImportResult(
    IReadOnlyList<Models.CallRecord> Records,
    IReadOnlyList<PdfImportWarning> Warnings);
