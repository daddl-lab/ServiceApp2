namespace ServiceApp.Core.Exceptions;

/// <summary>
/// Basisklasse aller Fehler, die beim Import und Parsen von Telefonbericht-PDFs auftreten
/// können. Ermöglicht der UI-Schicht, alle PDF-Import-Fehler mit einem einzigen
/// <c>catch (PdfImportException)</c> verständlich anzuzeigen, ohne jede konkrete Ursache
/// einzeln behandeln zu müssen.
/// </summary>
public abstract class PdfImportException : Exception
{
    protected PdfImportException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>Wird geworfen, wenn der konfigurierte PDF-Ordner einer Servicenummer nicht existiert.</summary>
public sealed class PdfFolderNotFoundException : PdfImportException
{
    public PdfFolderNotFoundException(string folderPath)
        : base($"Der konfigurierte Ordner wurde nicht gefunden: \"{folderPath}\". Bitte prüfen Sie den Pfad in den Einstellungen.")
    {
    }
}

/// <summary>Wird geworfen, wenn eine einzelne PDF-Datei nicht gelesen werden kann (Datei fehlt oder wurde entfernt).</summary>
public sealed class PdfNotFoundException : PdfImportException
{
    public PdfNotFoundException(string filePath)
        : base($"Die PDF-Datei wurde nicht gefunden: \"{filePath}\".")
    {
    }
}

/// <summary>
/// Wird geworfen, wenn eine PDF-Datei beschädigt ist oder aus anderen Gründen nicht als
/// PDF geöffnet werden kann (z. B. defekte Bytes, kein gültiges PDF-Format).
/// </summary>
public sealed class CorruptPdfException : PdfImportException
{
    public CorruptPdfException(string filePath, Exception innerException)
        : base($"Die PDF-Datei \"{filePath}\" ist beschädigt oder kein gültiges PDF und konnte nicht geöffnet werden.", innerException)
    {
    }
}

/// <summary>
/// Wird geworfen, wenn eine PDF-Datei zwar geöffnet werden kann, ihr Inhalt aber von
/// keinem der registrierten <see cref="PdfParser.IReportLineInterpreter"/> als
/// bekanntes Berichtsformat erkannt wird.
/// </summary>
public sealed class UnrecognizedReportFormatException : PdfImportException
{
    public UnrecognizedReportFormatException(string filePath)
        : base($"Das Format der PDF-Datei \"{filePath}\" konnte von keinem bekannten Parser gelesen werden. " +
               "Möglicherweise handelt es sich um ein neues Berichtsformat, für das noch kein Interpreter existiert.")
    {
    }
}

/// <summary>Wird geworfen, wenn beim Zugriff auf einen PDF-Ordner oder eine PDF-Datei die Berechtigung fehlt.</summary>
public sealed class PdfAccessDeniedException : PdfImportException
{
    public PdfAccessDeniedException(string path, Exception innerException)
        : base($"Zugriff auf \"{path}\" verweigert. Bitte prüfen Sie die Dateiberechtigungen.", innerException)
    {
    }
}
