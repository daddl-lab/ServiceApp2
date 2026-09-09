namespace ServiceApp.Core.Models;

/// <summary>
/// Laufender Fortschritt eines Index-Scans oder einer Live-Suche, gemeldet über
/// <see cref="IProgress{T}"/>, damit die Oberfläche z. B. "Durchsuchter Bereich: ..." und
/// die bisher gefundene Trefferzahl anzeigen kann, ohne auf den Abschluss zu warten.
/// </summary>
/// <param name="CurrentArea">Aktuell durchsuchter Verzeichnisbereich (zur Anzeige, z. B. relativer Pfad).</param>
/// <param name="ItemsProcessed">Anzahl bisher untersuchter Dateien/Einträge.</param>
public sealed record ArchiveScanStatus(string CurrentArea, long ItemsProcessed);
