namespace ServiceApp.Core.Models;

/// <summary>
/// Aktueller Zustand des lokalen Suchindex, wie er z. B. im Einstellungen-Bereich
/// angezeigt wird. <see cref="IndexReady"/> steuert außerdem, ob
/// <see cref="FileArchive.IFileArchiveSearchService"/> gegen den Index oder (nur vor dem
/// ersten vollständigen Scan) über einen eingegrenzten Live-Zugriff auf das Netzlaufwerk sucht.
/// </summary>
/// <param name="IndexReady">Ob mindestens ein vollständiger Scan seit dem letzten (Neu-)Aufbau des Index abgeschlossen wurde.</param>
/// <param name="IsScanning">Ob aktuell ein Scan (voll oder inkrementell) läuft.</param>
/// <param name="LastFullScanCompletedUtc">Zeitpunkt des letzten vollständig abgeschlossenen Scans.</param>
/// <param name="LastIncrementalScanCompletedUtc">Zeitpunkt der letzten abgeschlossenen inkrementellen Aktualisierung.</param>
/// <param name="TotalFileCount">Anzahl aktuell im Index enthaltener Dateien.</param>
/// <param name="LastScanErrorCount">Anzahl der beim letzten Scan übersprungenen Dateien/Ordner (z. B. wegen fehlender Berechtigung).</param>
/// <param name="LastScanDuration">Dauer des letzten Scans.</param>
public sealed record IndexStatus(
    bool IndexReady,
    bool IsScanning,
    DateTime? LastFullScanCompletedUtc,
    DateTime? LastIncrementalScanCompletedUtc,
    long TotalFileCount,
    long LastScanErrorCount,
    TimeSpan LastScanDuration)
{
    public static readonly IndexStatus Empty = new(false, false, null, null, 0, 0, TimeSpan.Zero);
}
