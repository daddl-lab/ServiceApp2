namespace ServiceApp.Desktop.Services;

/// <summary>
/// Abstrahiert das Öffnen von Dateien und Ordnern über die Windows-Shell (Doppelklick auf
/// einen Suchtreffer bzw. "Ordner öffnen" im Kontextmenü der Zeichnungsarchiv-Suche).
/// Durch dieses Interface bleiben ViewModels frei von direkten <see cref="System.Diagnostics.Process"/>-Aufrufen
/// und damit weiterhin ohne laufende Oberfläche testbar.
/// </summary>
public interface IShellLaunchService
{
    /// <summary>Öffnet eine Datei mit der Windows-Standardanwendung für ihren Dateityp.</summary>
    /// <exception cref="ShellLaunchException">Die Datei konnte nicht geöffnet werden (z. B. gelöscht, keine zugeordnete Anwendung).</exception>
    void OpenFile(string filePath);

    /// <summary>Öffnet den Ordner, der die angegebene Datei enthält, im Windows-Explorer und markiert die Datei darin.</summary>
    /// <exception cref="ShellLaunchException">Der Ordner konnte nicht geöffnet werden.</exception>
    void OpenContainingFolder(string filePath);
}

/// <summary>
/// Wird geworfen, wenn eine Datei oder ihr enthaltender Ordner nicht über die Shell
/// geöffnet werden konnte. Trägt bewusst eine für Benutzer verständliche Meldung statt
/// eines technischen Stacktraces.
/// </summary>
public sealed class ShellLaunchException : Exception
{
    public ShellLaunchException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
