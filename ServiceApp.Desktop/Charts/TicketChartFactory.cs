using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using ServiceApp.Core.Models;
using SkiaSharp;

namespace ServiceApp.Desktop.Charts;

/// <summary>
/// Erzeugt die LiveCharts2-Diagrammserien und -Achsen des Ticket-Dashboards. Getrennt
/// von <see cref="ChartFactory"/> (Telefonberichte), da beide Dashboards eigene,
/// unabhängige Diagrammtypen mit eigenen Farbschemata verwenden.
/// </summary>
public static class TicketChartFactory
{
    private static readonly SKColor TrendColor = new(0x15, 0x65, 0xC0);

    /// <summary>
    /// Qualitative Farbpalette für die Fehlerursachen-Slices des Kuchendiagramms. Grau
    /// ist bewusst nicht enthalten - dieser Farbton ist dem Sammeleintrag "Sonstige"
    /// vorbehalten (siehe <see cref="CreateCauseBreakdownPieSeries"/>).
    /// </summary>
    private static readonly SKColor[] CausePalette =
    {
        new(0x15, 0x65, 0xC0), // Blau
        new(0xF9, 0xA8, 0x25), // Orange
        new(0x2E, 0x7D, 0x32), // Grün
        new(0xC6, 0x28, 0x28), // Rot
        new(0x6A, 0x1B, 0x9A), // Violett
        new(0x00, 0x83, 0x8F), // Türkis
        new(0xEF, 0x6C, 0x00), // Dunkelorange
        new(0xAD, 0x14, 0x57)  // Magenta
    };

    private static readonly SKColor OtherCauseColor = new(0x75, 0x75, 0x75); // Grau, für "Sonstige"
    private const string OtherCauseLabel = "Sonstige";

    /// <summary>Balkendiagramm der Ticketanzahl je Zeitpunkt (Tag oder Monat, siehe <see cref="TicketTimeSeriesPoint"/>).</summary>
    public static ISeries[] CreateTimeSeriesSeries(IReadOnlyList<TicketTimeSeriesPoint> timeSeries)
    {
        return new ISeries[]
        {
            new ColumnSeries<int>
            {
                Name = "Tickets",
                Values = timeSeries.Select(p => p.Count).ToArray(),
                Fill = new SolidColorPaint(TrendColor)
            }
        };
    }

    /// <summary>Beschriftungen der X-Achse für den Ticket-Zeitverlauf.</summary>
    public static Axis[] CreateTimeSeriesXAxes(IReadOnlyList<TicketTimeSeriesPoint> timeSeries)
    {
        return new[]
        {
            new Axis
            {
                Labels = timeSeries.Select(p => p.Label).ToArray(),
                LabelsRotation = timeSeries.Count > 14 ? 60 : 0
            }
        };
    }

    /// <summary>Kuchendiagramm der Fehlerursachen-Verteilung.</summary>
    public static ISeries[] CreateCauseBreakdownPieSeries(IReadOnlyList<TicketCauseCount> causeBreakdown)
    {
        var series = new List<ISeries>(causeBreakdown.Count);
        var paletteIndex = 0;

        foreach (var cause in causeBreakdown)
        {
            var color = cause.Cause == OtherCauseLabel
                ? OtherCauseColor
                : CausePalette[paletteIndex++ % CausePalette.Length];

            series.Add(new PieSeries<double>
            {
                Name = cause.Cause,
                Values = new double[] { cause.Count },
                Fill = new SolidColorPaint(color)
            });
        }

        return series.ToArray();
    }
}
