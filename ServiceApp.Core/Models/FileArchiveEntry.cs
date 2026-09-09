namespace ServiceApp.Core.Models;

/// <summary>
/// Ein einzelner indexierter Treffer im Zeichnungsarchiv. Enthält sowohl die für die
/// Anzeige benötigten Metadaten als auch die aus dem Pfad abgeleiteten Ordnerstruktur-Ebenen
/// (siehe <see cref="FileArchive.ArchivePathResolver"/>), damit Index und Suche denselben
/// Datensatz für Anzeige und Ordnerstruktur-Filterung verwenden können.
/// </summary>
/// <param name="FullPath">Vollständiger Pfad der Datei, dient als eindeutiger Schlüssel im Index.</param>
/// <param name="FileName">Dateiname inklusive Endung.</param>
/// <param name="FileNameWithoutExtension">Dateiname ohne Endung, Ziel des Wildcard-Abgleichs.</param>
/// <param name="Extension">Dateiendung ohne führenden Punkt, klein geschrieben (z. B. "pdf").</param>
/// <param name="DirectoryPath">Verzeichnis, in dem sich die Datei befindet.</param>
/// <param name="Level1">Erste Ordnerstruktur-Ebene relativ zur Archivwurzel, oder <c>null</c>, wenn die Datei außerhalb der erwarteten 3-Ebenen-Struktur liegt.</param>
/// <param name="Level2">Zweite Ordnerstruktur-Ebene, oder <c>null</c>.</param>
/// <param name="Level3">Dritte Ordnerstruktur-Ebene, oder <c>null</c>.</param>
/// <param name="SizeBytes">Dateigröße in Bytes.</param>
/// <param name="LastWriteUtc">Letzter Änderungszeitpunkt (UTC).</param>
public sealed record FileArchiveEntry(
    string FullPath,
    string FileName,
    string FileNameWithoutExtension,
    string Extension,
    string DirectoryPath,
    string? Level1,
    string? Level2,
    string? Level3,
    long SizeBytes,
    DateTime LastWriteUtc);
