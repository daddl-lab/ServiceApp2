namespace ServiceApp.Core.Exceptions;

/// <summary>
/// Basisklasse aller Fehler, die beim Import der Serviceticket-Excel-Datei auftreten
/// können. Analog zu <see cref="PdfImportException"/> für den PDF-Import, ermöglicht sie
/// der UI-Schicht, alle Ticket-Import-Fehler mit einem einzigen
/// <c>catch (TicketImportException)</c> verständlich anzuzeigen.
/// </summary>
public abstract class TicketImportException : Exception
{
    protected TicketImportException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>Wird geworfen, wenn keine Excel-Datei konfiguriert oder die konfigurierte Datei nicht vorhanden ist.</summary>
public sealed class TicketFileNotFoundException : TicketImportException
{
    public TicketFileNotFoundException(string filePath)
        : base($"Die Excel-Datei wurde nicht gefunden: \"{filePath}\". Bitte prüfen Sie den Pfad in den Einstellungen.")
    {
    }
}

/// <summary>
/// Wird geworfen, wenn die Excel-Datei beschädigt ist oder aus anderen Gründen nicht als
/// Excel-Arbeitsmappe geöffnet werden kann.
/// </summary>
public sealed class CorruptTicketFileException : TicketImportException
{
    public CorruptTicketFileException(string filePath, Exception innerException)
        : base($"Die Excel-Datei \"{filePath}\" ist beschädigt oder keine gültige Excel-Datei und konnte nicht geöffnet werden.", innerException)
    {
    }
}

/// <summary>
/// Wird geworfen, wenn die Excel-Datei geöffnet werden kann, aber die für die Auswertung
/// benötigten Spalten (z. B. "Anlagedatum") nicht in der Kopfzeile gefunden wurden.
/// </summary>
public sealed class InvalidTicketFileFormatException : TicketImportException
{
    public InvalidTicketFileFormatException(string filePath, IReadOnlyList<string> missingColumns)
        : base($"Die Excel-Datei \"{filePath}\" enthält nicht die erwarteten Spalten: {string.Join(", ", missingColumns)}.")
    {
    }
}

/// <summary>Wird geworfen, wenn beim Zugriff auf die Excel-Datei die Berechtigung fehlt.</summary>
public sealed class TicketFileAccessDeniedException : TicketImportException
{
    public TicketFileAccessDeniedException(string filePath, Exception innerException)
        : base($"Zugriff auf \"{filePath}\" verweigert. Bitte prüfen Sie die Dateiberechtigungen.", innerException)
    {
    }
}
