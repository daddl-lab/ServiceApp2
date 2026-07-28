using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using ServiceApp.Core.Models;
using SkiaSharp;

namespace ServiceApp.Desktop.Charts;

/// <summary>
/// Erzeugt alle LiveCharts2-Diagrammserien und -Achsen des Dashboards aus den
/// berechneten Statistik-Modellen. Bündelt sämtliche Diagrammerzeugung an einer Stelle,
/// damit ViewModels sich ausschließlich um Zustand und Datenfluss kümmern und nicht um
/// LiveCharts-spezifische Konfiguration (Farben, Achsenformatierung, ...).
/// </summary>
public static class ChartFactory
{
    private static readonly SKColor AnsweredColor = new(0x2E, 0x7D, 0x32);
    private static readonly SKColor MissedColor = new(0xC6, 0x28, 0x28);
    private static readonly SKColor TotalColor = new(0x15, 0x65, 0xC0);
    private static readonly SKColor SecondSeriesColor = new(0xF9, 0xA8, 0x25);

    /// <summary>
    /// Diagramm des zeitlichen Verlaufs: eine Linie mit der Gesamtanzahl der Anrufe je
    /// Tag, ergänzt um zwei Balkenserien für angenommene und verpasste Anrufe je Tag,
    /// damit neben dem reinen Anrufvolumen auch dessen Zusammensetzung sichtbar wird.
    /// </summary>
    public static ISeries[] CreateDailyTrendSeries(IReadOnlyList<DailyCallCount> dailyCounts)
    {
        return new ISeries[]
        {
            new ColumnSeries<int>
            {
                Name = "Angenommen",
                Values = dailyCounts.Select(d => d.AnsweredCalls).ToArray(),
                Fill = new SolidColorPaint(AnsweredColor)
            },
            new ColumnSeries<int>
            {
                Name = "Verpasst",
                Values = dailyCounts.Select(d => d.MissedCalls).ToArray(),
                Fill = new SolidColorPaint(MissedColor)
            },
            new LineSeries<int>
            {
                Name = "Gesamt",
                Values = dailyCounts.Select(d => d.TotalCalls).ToArray(),
                Fill = null,
                Stroke = new SolidColorPaint(TotalColor) { StrokeThickness = 2 },
                GeometrySize = 4,
                GeometryStroke = new SolidColorPaint(TotalColor)
            }
        };
    }

    /// <summary>Beschriftungen der X-Achse für den Tagesverlauf (ein Label je Kalendertag).</summary>
    public static Axis[] CreateDailyTrendXAxes(IReadOnlyList<DailyCallCount> dailyCounts)
    {
        return new[]
        {
            new Axis
            {
                Labels = dailyCounts.Select(d => d.Date.ToString("dd.MM.")).ToArray(),
                LabelsRotation = dailyCounts.Count > 14 ? 60 : 0
            }
        };
    }

    /// <summary>
    /// Häufigkeitsdiagramm (Stoßzeiten): Anzahl der Anrufe je Stunde. Wird sowohl als
    /// Balken- als auch als Liniendiagramm unterstützt (<paramref name="asLineChart"/>),
    /// damit die Oberfläche zwischen beiden Darstellungen umschalten kann.
    /// </summary>
    public static ISeries[] CreateHourlyFrequencySeries(IReadOnlyList<HourlyCallCount> hourlyDistribution, bool asLineChart)
    {
        var values = hourlyDistribution.Select(h => h.TotalCalls).ToArray();

        if (asLineChart)
        {
            return new ISeries[]
            {
                new LineSeries<int>
                {
                    Name = "Anrufe je Stunde",
                    Values = values,
                    Fill = null,
                    Stroke = new SolidColorPaint(TotalColor) { StrokeThickness = 2 },
                    GeometrySize = 3
                }
            };
        }

        return new ISeries[]
        {
            new ColumnSeries<int>
            {
                Name = "Anrufe je Stunde",
                Values = values,
                Fill = new SolidColorPaint(TotalColor)
            }
        };
    }

    /// <summary>
    /// Häufigkeitsdiagramm mit je einer Serie pro Servicenummer, für die
    /// "Servicenummer einzeln"-Ansicht (zwei Balken- oder Linienserien in einem
    /// Diagramm statt eines gemeinsamen Summenwerts).
    /// </summary>
    public static ISeries[] CreateHourlyFrequencyComparisonSeries(
        string firstName, IReadOnlyList<HourlyCallCount> firstHourly,
        string secondName, IReadOnlyList<HourlyCallCount> secondHourly,
        bool asLineChart)
    {
        var firstValues = firstHourly.Select(h => h.TotalCalls).ToArray();
        var secondValues = secondHourly.Select(h => h.TotalCalls).ToArray();

        if (asLineChart)
        {
            return new ISeries[]
            {
                new LineSeries<int>
                {
                    Name = firstName,
                    Values = firstValues,
                    Fill = null,
                    Stroke = new SolidColorPaint(TotalColor) { StrokeThickness = 2 },
                    GeometrySize = 3
                },
                new LineSeries<int>
                {
                    Name = secondName,
                    Values = secondValues,
                    Fill = null,
                    Stroke = new SolidColorPaint(SecondSeriesColor) { StrokeThickness = 2 },
                    GeometrySize = 3
                }
            };
        }

        return new ISeries[]
        {
            new ColumnSeries<int>
            {
                Name = firstName,
                Values = firstValues,
                Fill = new SolidColorPaint(TotalColor)
            },
            new ColumnSeries<int>
            {
                Name = secondName,
                Values = secondValues,
                Fill = new SolidColorPaint(SecondSeriesColor)
            }
        };
    }

    /// <summary>Beschriftungen der X-Achse für die Stundenverteilung (00 bis 23 Uhr).</summary>
    public static Axis[] CreateHourlyXAxes()
    {
        return new[]
        {
            new Axis
            {
                Labels = Enumerable.Range(0, 24).Select(h => $"{h:00}").ToArray()
            }
        };
    }

    /// <summary>Kreisdiagramm für angenommene vs. verpasste Anrufe.</summary>
    public static ISeries[] CreateAnsweredVsMissedPieSeries(int answered, int missed)
    {
        return new ISeries[]
        {
            new PieSeries<double>
            {
                Name = "Angenommen",
                Values = new double[] { answered },
                Fill = new SolidColorPaint(AnsweredColor)
            },
            new PieSeries<double>
            {
                Name = "Verpasst",
                Values = new double[] { missed },
                Fill = new SolidColorPaint(MissedColor)
            }
        };
    }

    /// <summary>
    /// Verlaufsvergleich beider Servicenummern als zwei überlagerte Linienserien über
    /// denselben Zeitraum. Wird für die "Beide einzeln"-Ansicht des Zeitlicher-Verlauf-
    /// Diagramms verwendet.
    /// </summary>
    public static ISeries[] CreateComparisonTrendSeries(
        string firstName, IReadOnlyList<DailyCallCount> firstDaily,
        string secondName, IReadOnlyList<DailyCallCount> secondDaily)
    {
        return new ISeries[]
        {
            new LineSeries<int>
            {
                Name = firstName,
                Values = firstDaily.Select(d => d.TotalCalls).ToArray(),
                Fill = null,
                Stroke = new SolidColorPaint(TotalColor) { StrokeThickness = 2 },
                GeometrySize = 3
            },
            new LineSeries<int>
            {
                Name = secondName,
                Values = secondDaily.Select(d => d.TotalCalls).ToArray(),
                Fill = null,
                Stroke = new SolidColorPaint(SecondSeriesColor) { StrokeThickness = 2 },
                GeometrySize = 3
            }
        };
    }
}
