namespace ServiceApp.Core.Exceptions;

/// <summary>
/// Basisklasse aller Fehler rund um das Zeichnungsarchiv (Indexierung und Suche).
/// Ermöglicht der UI-Schicht, alle Archiv-Fehler mit einem einzigen
/// <c>catch (FileArchiveException)</c> verständlich anzuzeigen, ohne jede konkrete
/// Ursache einzeln behandeln zu müssen.
/// </summary>
public abstract class FileArchiveException : Exception
{
    protected FileArchiveException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>Wird geworfen, wenn der konfigurierte Archivpfad nicht (mehr) existiert.</summary>
public sealed class ArchiveRootNotFoundException : FileArchiveException
{
    public ArchiveRootNotFoundException(string archivePath)
        : base($"Der konfigurierte Archivpfad wurde nicht gefunden: \"{archivePath}\". Bitte prüfen Sie den Pfad in den Einstellungen.")
    {
    }
}

/// <summary>
/// Wird geworfen, wenn das Netzlaufwerk bzw. der Server, auf dem sich das Archiv
/// befindet, nicht erreichbar ist (z. B. Server offline, Netzwerkunterbrechung).
/// </summary>
public sealed class ArchiveUnreachableException : FileArchiveException
{
    public ArchiveUnreachableException(string archivePath, Exception innerException)
        : base($"Das Netzlaufwerk \"{archivePath}\" ist derzeit nicht erreichbar. Bitte prüfen Sie die Netzwerkverbindung und versuchen Sie es erneut.", innerException)
    {
    }
}

/// <summary>Wird geworfen, wenn beim Zugriff auf den Archivpfad die Berechtigung fehlt.</summary>
public sealed class ArchiveAccessDeniedException : FileArchiveException
{
    public ArchiveAccessDeniedException(string path, Exception innerException)
        : base($"Zugriff auf \"{path}\" verweigert. Bitte prüfen Sie Ihre Berechtigungen für das Zeichnungsarchiv.", innerException)
    {
    }
}

/// <summary>
/// Wird geworfen, wenn der lokale Suchindex beschädigt ist und nicht gelesen werden kann.
/// Der Index wird in diesem Fall automatisch verworfen und bei der nächsten Gelegenheit
/// vollständig neu aufgebaut - diese Ausnahme dient primär der Protokollierung.
/// </summary>
public sealed class IndexCorruptedException : FileArchiveException
{
    public IndexCorruptedException(string indexPath, Exception innerException)
        : base($"Der Suchindex \"{indexPath}\" ist beschädigt und wird neu aufgebaut.", innerException)
    {
    }
}

/// <summary>
/// Wird geworfen, wenn ein eingegebener Suchbegriff bzw. eine Platzhalterkombination
/// ungültig ist (z. B. leerer Begriff).
/// </summary>
public sealed class InvalidSearchTermException : FileArchiveException
{
    public InvalidSearchTermException(string message)
        : base(message)
    {
    }
}
