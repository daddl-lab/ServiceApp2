namespace ServiceApp.Desktop.ViewModels;

/// <summary>
/// Steuert, welche Servicenummer(n) in den Diagrammen "Zeitlicher Verlauf",
/// "Häufigkeit der Anrufe" und "Angenommen / Verpasst" dargestellt werden.
/// </summary>
public enum ChartViewMode
{
    /// <summary>Summe beider Servicenummern in einer Serie (Standard).</summary>
    Combined,

    /// <summary>Beide Servicenummern einzeln als zwei Serien im selben Diagramm.</summary>
    Separate,

    /// <summary>Nur die erste Servicenummer.</summary>
    FirstOnly,

    /// <summary>Nur die zweite Servicenummer.</summary>
    SecondOnly
}
