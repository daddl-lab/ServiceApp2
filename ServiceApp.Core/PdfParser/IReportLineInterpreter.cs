using ServiceApp.Core.Models;

namespace ServiceApp.Core.PdfParser;

/// <summary>
/// Ergebnis der erfolgreichen Interpretation einer einzelnen Textzeile eines
/// Telefonberichts, bevor sie mit Servicenummer und Quelldatei zu einem vollständigen
/// <see cref="CallRecord"/> angereichert wird.
/// </summary>
/// <param name="Timestamp">Erkannter Zeitpunkt des Anrufs.</param>
/// <param name="Status">Erkannter Anrufstatus (angenommen/verpasst).</param>
/// <param name="DurationSeconds">Erkannte Gesprächsdauer in Sekunden, falls in der Zeile vorhanden.</param>
public readonly record struct InterpretedLine(DateTime Timestamp, CallStatus Status, int? DurationSeconds);

/// <summary>
/// Erkennt und interpretiert eine einzelne Textzeile eines PDF-Berichts in einem
/// bestimmten, bekannten Tabellenformat (z. B. deutsches Datumsformat vs. ISO-Format).
/// Dies ist die zentrale Erweiterungsstelle, um neue Berichts-Layouts zu unterstützen:
/// eine neue Implementierung dieses Interfaces genügt, ohne dass
/// <see cref="PdfPigReportParser"/> angepasst werden muss (Open/Closed-Prinzip).
/// </summary>
public interface IReportLineInterpreter
{
    /// <summary>Sprechender Name des Formats, das dieser Interpreter erkennt (für Logging/Diagnose).</summary>
    string FormatName { get; }

    /// <summary>
    /// Versucht, die übergebene, aus dem PDF rekonstruierte Textzeile als Anrufdatensatz
    /// zu interpretieren.
    /// </summary>
    /// <param name="line">Eine einzelne, positionsbasiert rekonstruierte Zeile aus dem PDF.</param>
    /// <param name="result">Das Interpretationsergebnis, falls die Zeile erkannt wurde.</param>
    /// <returns><c>true</c>, wenn die Zeile als Anrufdatensatz dieses Formats erkannt wurde.</returns>
    bool TryInterpret(string line, out InterpretedLine result);
}
