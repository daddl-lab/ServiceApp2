using ServiceApp.Core.Models;

namespace ServiceApp.Core.Repository;

/// <summary>
/// Lädt alle Anrufdatensätze einer Servicenummer, indem alle PDF-Berichte im
/// konfigurierten Ordner eingelesen werden. Kapselt den Ordnerzugriff und die
/// Fehlerisolierung pro Datei, sodass die aufrufende Schicht (Statistik, ViewModels)
/// nicht wissen muss, dass die Daten aus mehreren PDF-Dateien stammen.
/// </summary>
public interface ICallRecordRepository
{
    /// <summary>
    /// Liest alle PDF-Dateien im konfigurierten Ordner der Servicenummer ein.
    /// Einzelne fehlerhafte Dateien werden übersprungen und als
    /// <see cref="PdfImportWarning"/> zurückgegeben, statt den gesamten Import
    /// abzubrechen (siehe <see cref="CallRecordImportResult"/>).
    /// </summary>
    /// <exception cref="Exceptions.PdfFolderNotFoundException">Der konfigurierte Ordner existiert nicht.</exception>
    /// <exception cref="Exceptions.PdfAccessDeniedException">Der Ordner kann nicht gelesen werden (Berechtigung).</exception>
    CallRecordImportResult GetCallRecords(ServiceNumberSettings serviceNumber);
}
