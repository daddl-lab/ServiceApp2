namespace ServiceApp.Core.Models;

/// <summary>
/// Auswählbare Dateityp-Einschränkung für die Zeichnungsarchiv-Suche. Der in der
/// Aufgabenstellung geforderte Dateityp "DWK" wird als DWG (AutoCAD-Zeichnung) geführt -
/// DWK ist kein gängiges CAD-/Office-Format und wurde als Tippfehler interpretiert.
/// </summary>
public enum FileTypeFilter
{
    All,
    Tif,
    Pdf,
    Jt,
    Dwg,
    Doc,
    Docx,
    Dxf,
    Xls,
    Xlsx
}

/// <summary>Hilfsfunktionen zur Anzeige und zum endungsbasierten Abgleich von <see cref="FileTypeFilter"/>.</summary>
public static class FileTypeFilterExtensions
{
    private static readonly Dictionary<FileTypeFilter, string> Extensions = new()
    {
        [FileTypeFilter.Tif] = "tif",
        [FileTypeFilter.Pdf] = "pdf",
        [FileTypeFilter.Jt] = "jt",
        [FileTypeFilter.Dwg] = "dwg",
        [FileTypeFilter.Doc] = "doc",
        [FileTypeFilter.Docx] = "docx",
        [FileTypeFilter.Dxf] = "dxf",
        [FileTypeFilter.Xls] = "xls",
        [FileTypeFilter.Xlsx] = "xlsx"
    };

    /// <summary>Anzeigename für die Dateityp-Auswahlliste in der Oberfläche.</summary>
    public static string DisplayName(this FileTypeFilter filter) => filter switch
    {
        FileTypeFilter.All => "Alle Dateitypen",
        _ => Extensions[filter].ToUpperInvariant()
    };

    /// <summary>
    /// Prüft, ob eine (ohne führenden Punkt übergebene) Dateiendung zu diesem Filter passt.
    /// Der Vergleich ist unabhängig von Groß-/Kleinschreibung, wie von der
    /// Aufgabenstellung gefordert (".pdf", ".PDF", ".Pdf" gelten als identisch).
    /// </summary>
    public static bool MatchesExtension(this FileTypeFilter filter, string extensionWithoutDot)
        => filter == FileTypeFilter.All
            || string.Equals(Extensions[filter], extensionWithoutDot, StringComparison.OrdinalIgnoreCase);

    /// <summary>Alle vom Archiv unterstützten Dateiendungen (ohne Punkt, klein geschrieben) - definiert, welche Dateien überhaupt indexiert werden.</summary>
    public static IReadOnlyCollection<string> AllSupportedExtensions { get; } = Extensions.Values.ToArray();
}
