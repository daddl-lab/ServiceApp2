namespace ServiceApp.Core.Models;

/// <summary>
/// Eine an <see cref="FileArchive.IFileArchiveSearchService"/> übergebene Suchanfrage.
/// </summary>
/// <param name="SearchTerm">
/// Suchbegriff, optional mit den Platzhaltern <c>*</c> (beliebig lange Zeichenfolge) und
/// <c>?</c> (genau ein Zeichen). Ohne Platzhalter wird exakt (case-insensitive) gegen den
/// Dateinamen ohne Endung verglichen.
/// </param>
/// <param name="FileType">Einschränkung auf einen Dateityp, oder <see cref="FileTypeFilter.All"/> für alle unterstützten Typen.</param>
/// <param name="CaseSensitive">Wenn <c>true</c>, wird der Suchbegriff exakt in Groß-/Kleinschreibung abgeglichen (Standard: case-insensitiv).</param>
/// <param name="MaxResults">Obergrenze der zurückgelieferten Treffer, um Oberfläche und Speicherverbrauch bei sehr vielen Treffern zu schützen.</param>
public sealed record ArchiveSearchQuery(
    string SearchTerm,
    FileTypeFilter FileType,
    bool CaseSensitive,
    int MaxResults);
