namespace ServiceApp.Core.Models;

/// <summary>
/// Ergebnis von <see cref="FileArchive.ArchivePathResolver"/>: beschreibt, wie weit sich
/// der Suchbereich anhand der bekannten 3-Ebenen-Ordnerstruktur des Archivs (Ebene 1/2/3,
/// Länge über <see cref="AppSettings.FileArchiveLevelLengths"/> konfigurierbar) eingrenzen
/// lässt, bevor auf Basis des Suchbegriffs überhaupt auf das Dateisystem zugegriffen wird.
/// </summary>
public enum ArchiveNarrowingKind
{
    /// <summary>Alle drei Ebenen sind aus dem Suchbegriff literal bekannt - genau ein Zielverzeichnis.</summary>
    Exact,

    /// <summary>Ebene 1 und 2 sind literal bekannt, Ebene 3 enthält einen Platzhalter und muss über einen Verzeichnisnamen-Abgleich aufgelöst werden.</summary>
    PartialLevel3,

    /// <summary>Nur Ebene 1 ist literal bekannt, Ebene 2 enthält einen Platzhalter, Ebene 3 ist vollständig unbekannt.</summary>
    PartialLevel2,

    /// <summary>Der Suchbegriff lässt keinerlei Eingrenzung zu (z. B. führender Platzhalter, zu kurzer Begriff, Sonderzeichen).</summary>
    Unresolvable
}

/// <param name="Kind">Art der Eingrenzung, siehe <see cref="ArchiveNarrowingKind"/>.</param>
/// <param name="Level1">Literaler Name von Ebene 1, sofern bekannt.</param>
/// <param name="Level2">Literaler Name von Ebene 2, sofern bekannt.</param>
/// <param name="Level3">Literaler Name von Ebene 3, sofern <see cref="Kind"/> == <see cref="ArchiveNarrowingKind.Exact"/>.</param>
/// <param name="WildcardLevelNamePattern">
/// Platzhalter-Muster (im selben <c>*</c>/<c>?</c>-Format wie der Suchbegriff) für die erste
/// noch nicht literal bekannte Ebene, gegen das tatsächliche Unterverzeichnisnamen abgeglichen
/// werden müssen. Nur bei <see cref="ArchiveNarrowingKind.PartialLevel2"/> und
/// <see cref="ArchiveNarrowingKind.PartialLevel3"/> gesetzt.
/// </param>
public sealed record ArchiveNarrowingPlan(
    ArchiveNarrowingKind Kind,
    string? Level1,
    string? Level2,
    string? Level3,
    string? WildcardLevelNamePattern)
{
    public static readonly ArchiveNarrowingPlan Unresolvable = new(ArchiveNarrowingKind.Unresolvable, null, null, null, null);
}
