using ServiceApp.Core.Models;

namespace ServiceApp.Core.Configuration;

/// <summary>
/// Lädt und speichert die Anwendungseinstellungen (<see cref="AppSettings"/>) dauerhaft,
/// sodass sie beim nächsten Programmstart automatisch wieder zur Verfügung stehen.
/// Die konkrete Speicherform (JSON-Datei, Datenbank, ...) ist ein Implementierungsdetail
/// hinter diesem Interface und für die aufrufenden ViewModels irrelevant.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Lädt die gespeicherten Einstellungen. Existiert noch keine Einstellungsdatei
    /// (z. B. beim allerersten Programmstart), werden Standardeinstellungen
    /// zurückgegeben, ohne dass ein Fehler geworfen wird.
    /// </summary>
    AppSettings Load();

    /// <summary>Speichert die übergebenen Einstellungen dauerhaft.</summary>
    void Save(AppSettings settings);
}
