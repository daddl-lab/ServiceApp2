using System.ComponentModel;
using System.Diagnostics;

namespace ServiceApp.Desktop.Services;

/// <summary>
/// Implementiert <see cref="IShellLaunchService"/> über <see cref="Process.Start(ProcessStartInfo)"/>
/// mit <c>UseShellExecute = true</c> - das lässt Windows die Datei mit der für ihren Typ
/// registrierten Standardanwendung öffnen bzw. den Explorer mit der Datei markiert starten,
/// exakt wie ein Doppelklick im Explorer selbst. Fehler (Datei zwischenzeitlich gelöscht,
/// keine zugeordnete Anwendung, keine Berechtigung) werden in eine verständliche
/// <see cref="ShellLaunchException"/> übersetzt, statt den technischen Stacktrace an die
/// Oberfläche durchzureichen.
/// </summary>
public sealed class ShellLaunchService : IShellLaunchService
{
    public void OpenFile(string filePath)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new ShellLaunchException("Das Öffnen von Dateien wird nur unter Windows unterstützt.");
        }

        if (!File.Exists(filePath))
        {
            throw new ShellLaunchException($"Die Datei \"{filePath}\" wurde nicht gefunden. Möglicherweise wurde sie inzwischen verschoben oder gelöscht.");
        }

        try
        {
            Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true })?.Dispose();
        }
        catch (Win32Exception ex)
        {
            throw new ShellLaunchException($"Die Datei \"{filePath}\" konnte nicht geöffnet werden. Möglicherweise ist keine passende Anwendung dafür installiert.", ex);
        }
        catch (FileNotFoundException ex)
        {
            throw new ShellLaunchException($"Die Datei \"{filePath}\" wurde nicht gefunden.", ex);
        }
    }

    public void OpenContainingFolder(string filePath)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new ShellLaunchException("Das Öffnen von Ordnern wird nur unter Windows unterstützt.");
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{filePath}\"") { UseShellExecute = true })?.Dispose();
        }
        catch (Win32Exception ex)
        {
            throw new ShellLaunchException($"Der Ordner für \"{filePath}\" konnte nicht geöffnet werden.", ex);
        }
    }
}
