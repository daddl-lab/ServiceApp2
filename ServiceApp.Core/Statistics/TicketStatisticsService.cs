using ServiceApp.Core.Models;

namespace ServiceApp.Core.Statistics;

/// <summary>
/// Standard-Implementierung von <see cref="ITicketStatisticsService"/>.
/// </summary>
public sealed class TicketStatisticsService : ITicketStatisticsService
{
    /// <summary>
    /// Ab dieser Zeitraumlänge (in Tagen) wird der zeitliche Verlauf nach Monaten statt
    /// nach Tagen gebündelt (z. B. bei "Jahr" oder einem langen benutzerdefinierten
    /// Zeitraum), damit die Diagrammachse nicht mit hunderten Tagespunkten überladen
    /// wird.
    /// </summary>
    private const int MonthlyBucketThresholdDays = 62;

    /// <summary>
    /// Maximale Anzahl unterschiedlicher Ursachen, die einzeln im Kuchendiagramm
    /// gezeigt werden; alles darüber hinaus wird zu "Sonstige" zusammengefasst.
    /// </summary>
    private const int MaxCauseSlices = 9;

    private const string UnspecifiedCauseLabel = "Nicht angegeben";
    private const string OtherCauseLabel = "Sonstige";

    /// <inheritdoc />
    public TicketStatistics Compute(IReadOnlyList<ServiceTicket> tickets, DateRangeFilter range)
    {
        var ticketsInRange = tickets
            .Where(t => range.Contains(DateOnly.FromDateTime(t.CreatedAt)))
            .ToList();

        return new TicketStatistics
        {
            TotalTickets = ticketsInRange.Count,
            TimeSeries = BuildTimeSeries(ticketsInRange, range),
            CauseBreakdown = BuildCauseBreakdown(ticketsInRange)
        };
    }

    private static List<TicketTimeSeriesPoint> BuildTimeSeries(IReadOnlyList<ServiceTicket> tickets, DateRangeFilter range)
    {
        var spanDays = range.End.ToDateTime(TimeOnly.MinValue).Subtract(range.Start.ToDateTime(TimeOnly.MinValue)).Days + 1;

        return spanDays > MonthlyBucketThresholdDays
            ? BuildMonthlyTimeSeries(tickets, range)
            : BuildDailyTimeSeries(tickets, range);
    }

    private static List<TicketTimeSeriesPoint> BuildDailyTimeSeries(IReadOnlyList<ServiceTicket> tickets, DateRangeFilter range)
    {
        var counts = tickets
            .GroupBy(t => DateOnly.FromDateTime(t.CreatedAt))
            .ToDictionary(g => g.Key, g => g.Count());

        var result = new List<TicketTimeSeriesPoint>();
        foreach (var day in range.EnumerateDays())
        {
            counts.TryGetValue(day, out var count);
            result.Add(new TicketTimeSeriesPoint(day, day.ToString("dd.MM."), count));
        }

        return result;
    }

    private static List<TicketTimeSeriesPoint> BuildMonthlyTimeSeries(IReadOnlyList<ServiceTicket> tickets, DateRangeFilter range)
    {
        var counts = tickets
            .GroupBy(t => new DateOnly(t.CreatedAt.Year, t.CreatedAt.Month, 1))
            .ToDictionary(g => g.Key, g => g.Count());

        var result = new List<TicketTimeSeriesPoint>();
        var month = new DateOnly(range.Start.Year, range.Start.Month, 1);
        var lastMonth = new DateOnly(range.End.Year, range.End.Month, 1);

        while (month <= lastMonth)
        {
            counts.TryGetValue(month, out var count);
            result.Add(new TicketTimeSeriesPoint(month, month.ToString("MM.yyyy"), count));
            month = month.AddMonths(1);
        }

        return result;
    }

    /// <summary>
    /// Gruppiert Tickets nach Fehlerursache. Fehlt die Ursache, zählt das Ticket als
    /// "Nicht angegeben". Damit das Kuchendiagramm bei vielen unterschiedlichen
    /// Ursachen (in der Praxis 70+) lesbar bleibt, werden nur die
    /// <see cref="MaxCauseSlices"/> häufigsten einzeln ausgewiesen; der Rest wird zu
    /// "Sonstige" zusammengefasst.
    /// </summary>
    private static List<TicketCauseCount> BuildCauseBreakdown(IReadOnlyList<ServiceTicket> tickets)
    {
        var grouped = tickets
            .GroupBy(t => string.IsNullOrWhiteSpace(t.Cause) ? UnspecifiedCauseLabel : t.Cause.Trim())
            .Select(g => new TicketCauseCount(g.Key, g.Count()))
            .OrderByDescending(c => c.Count)
            .ToList();

        if (grouped.Count <= MaxCauseSlices)
        {
            return grouped;
        }

        var top = grouped.Take(MaxCauseSlices - 1).ToList();
        var otherCount = grouped.Skip(MaxCauseSlices - 1).Sum(c => c.Count);
        top.Add(new TicketCauseCount(OtherCauseLabel, otherCount));
        return top;
    }
}
