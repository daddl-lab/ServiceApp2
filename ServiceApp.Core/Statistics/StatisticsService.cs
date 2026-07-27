using ServiceApp.Core.Models;

namespace ServiceApp.Core.Statistics;

/// <summary>
/// Standard-Implementierung von <see cref="IStatisticsService"/>. Enthält die gesamte
/// Berechnungslogik für das Dashboard: zeitlicher Verlauf, Stundenverteilung,
/// Annahme-/Verpasst-Quote sowie die abgeleiteten Kennzahlen (bester/schwächster Tag,
/// Durchschnittswerte).
/// </summary>
public sealed class StatisticsService : IStatisticsService
{
    /// <inheritdoc />
    public ServiceNumberStatistics Compute(
        string serviceNumberId,
        string serviceNumberName,
        IReadOnlyList<CallRecord> records,
        DateRangeFilter range)
    {
        var recordsInRange = records
            .Where(r => range.Contains(DateOnly.FromDateTime(r.Timestamp)))
            .ToList();

        var totalCalls = recordsInRange.Count;
        var answeredCalls = recordsInRange.Count(r => r.IsAnswered);
        var missedCalls = totalCalls - answeredCalls;

        var dailyCounts = BuildDailyCounts(recordsInRange, range);
        var hourlyDistribution = BuildHourlyDistribution(recordsInRange);

        // Für Durchschnitts-/Bestwerte zählen nur Tage, an denen tatsächlich Anrufe
        // eingingen - ein langer Zeitraum mit vielen anruffreien Tagen (z. B. Wochenenden)
        // soll den Durchschnitt pro Tag nicht künstlich nach unten verzerren.
        var daysWithCalls = dailyCounts.Where(d => d.TotalCalls > 0).ToList();

        var averageCallsPerDay = daysWithCalls.Count == 0
            ? 0
            : daysWithCalls.Average(d => d.TotalCalls);

        var averageAnswerRate = daysWithCalls.Count == 0
            ? 0
            : daysWithCalls.Average(d => d.AnswerRatePercent);

        DailyCallCount? bestDay = daysWithCalls.Count == 0
            ? null
            : daysWithCalls.MaxBy(d => d.TotalCalls);

        DailyCallCount? worstDay = daysWithCalls.Count == 0
            ? null
            : daysWithCalls.MinBy(d => d.TotalCalls);

        return new ServiceNumberStatistics
        {
            ServiceNumberId = serviceNumberId,
            ServiceNumberName = serviceNumberName,
            TotalCalls = totalCalls,
            AnsweredCalls = answeredCalls,
            MissedCalls = missedCalls,
            DailyCounts = dailyCounts,
            HourlyDistribution = hourlyDistribution,
            AverageCallsPerDay = averageCallsPerDay,
            BestDay = bestDay,
            WorstDay = worstDay,
            AverageAnswerRatePercent = averageAnswerRate
        };
    }

    /// <inheritdoc />
    public ServiceNumberComparison Compare(ServiceNumberStatistics first, ServiceNumberStatistics second)
        => new(first, second);

    /// <summary>
    /// Erzeugt für jeden Tag des Zeitraums einen Eintrag - auch für Tage ohne Anrufe
    /// (mit Zählerstand 0). So zeigt das Zeitverlaufsdiagramm lückenlose Tagesreihen
    /// statt "übersprungener" Tage.
    /// </summary>
    private static List<DailyCallCount> BuildDailyCounts(IReadOnlyList<CallRecord> records, DateRangeFilter range)
    {
        var grouped = records
            .GroupBy(r => DateOnly.FromDateTime(r.Timestamp))
            .ToDictionary(g => g.Key, g => (Total: g.Count(), Answered: g.Count(r => r.IsAnswered)));

        var result = new List<DailyCallCount>();
        foreach (var day in range.EnumerateDays())
        {
            grouped.TryGetValue(day, out var counts);
            result.Add(new DailyCallCount(day, counts.Total, counts.Answered, counts.Total - counts.Answered));
        }

        return result;
    }

    /// <summary>
    /// Summiert Anrufe je Stunde (0-23) über den gesamten Zeitraum hinweg, unabhängig
    /// vom Kalendertag. Ergebnis enthält alle 24 Stunden (auch mit 0 Anrufen), damit
    /// Diagramme eine vollständige Tagesachse zeigen können.
    /// </summary>
    private static List<HourlyCallCount> BuildHourlyDistribution(IReadOnlyList<CallRecord> records)
    {
        var counts = new int[24];
        foreach (var record in records)
        {
            counts[record.Timestamp.Hour]++;
        }

        return Enumerable.Range(0, 24)
            .Select(hour => new HourlyCallCount(hour, counts[hour]))
            .ToList();
    }
}
