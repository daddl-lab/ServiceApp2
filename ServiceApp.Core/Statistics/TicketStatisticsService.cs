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
    /// Mindestanteil an den Gesamttickets, den eine Fehlerursache haben muss, um im
    /// Kuchendiagramm einzeln ausgewiesen zu werden; alles darunter wird zu "Sonstige"
    /// zusammengefasst.
    /// </summary>
    private const double OtherCauseShareThreshold = 0.02;

    private const string UnspecifiedCauseLabel = "Nicht angegeben";
    private const string OtherCauseLabel = "Sonstige";

    /// <summary>Bezeichnung, unter der Tickets ohne Typ-Angabe im Filter geführt werden.</summary>
    private const string UnspecifiedTypeLabel = "Nicht angegeben";

    /// <summary>Bezeichnung, unter der Tickets ohne Störungsort-Angabe geführt werden.</summary>
    private const string UnspecifiedLocationLabel = "Nicht angegeben";

    /// <summary>Bezeichnung, unter der Tickets ohne Kunden-Angabe (Adresszeile 1) geführt werden.</summary>
    private const string UnspecifiedCustomerLabel = "Nicht angegeben";

    /// <summary>Wert der Spalte "Externer Status", der ein abgeschlossenes Ticket kennzeichnet.</summary>
    private const string ClosedExternalStatus = "Abgeschlossen";

    /// <summary>
    /// Feste, aufsteigend sortierte Bearbeitszeit-Bereiche (in Tagen, Letzte Änderung
    /// − Anlagedatum) für das Bearbeitszeit-Balkendiagramm. <c>MaxDays</c> ist die
    /// (inklusive) Obergrenze des Bereichs; <c>null</c> markiert den letzten,
    /// unbegrenzten Bereich ("Rest").
    /// </summary>
    private static readonly (string Label, double? MaxDays)[] ProcessingTimeBuckets =
    {
        ("1 Tag", 1),
        ("2 Tage", 2),
        ("3 Tage", 3),
        ("1 Woche", 7),
        ("2 Wochen", 14),
        ("1 Monat", 30),
        ("2 Monate", 60),
        ("Rest", null)
    };

    /// <inheritdoc />
    public TicketStatistics Compute(
        IReadOnlyList<ServiceTicket> tickets,
        DateRangeFilter range,
        IReadOnlyCollection<string>? selectedTypes = null,
        int errorLocationTopCount = 8,
        int customerTopCount = 15)
    {
        var ticketsInRange = tickets
            .Where(t => range.Contains(DateOnly.FromDateTime(t.CreatedAt)))
            .Where(t => selectedTypes is null || selectedTypes.Count == 0 || selectedTypes.Contains(NormalizeType(t.Type)))
            .ToList();

        return new TicketStatistics
        {
            TotalTickets = ticketsInRange.Count,
            TimeSeries = BuildTimeSeries(ticketsInRange, range),
            CauseBreakdown = BuildCauseBreakdown(ticketsInRange),
            ErrorLocationBreakdown = BuildErrorLocationBreakdown(ticketsInRange, errorLocationTopCount),
            CustomerBreakdown = BuildCustomerBreakdown(ticketsInRange, customerTopCount),
            ProcessingTimeBreakdown = BuildProcessingTimeBreakdown(ticketsInRange)
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetDistinctTypes(IReadOnlyList<ServiceTicket> tickets)
    {
        return tickets
            .Select(t => NormalizeType(t.Type))
            .Distinct()
            .OrderBy(t => t, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static string NormalizeType(string? type) => string.IsNullOrWhiteSpace(type) ? UnspecifiedTypeLabel : type.Trim();

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
    /// Ursachen (in der Praxis 70+) lesbar bleibt, werden nur Ursachen mit mindestens
    /// <see cref="OtherCauseShareThreshold"/> Anteil an den Gesamttickets einzeln
    /// ausgewiesen; der Rest wird zu "Sonstige" zusammengefasst.
    /// </summary>
    private static List<TicketCauseCount> BuildCauseBreakdown(IReadOnlyList<ServiceTicket> tickets)
    {
        var grouped = tickets
            .GroupBy(t => string.IsNullOrWhiteSpace(t.Cause) ? UnspecifiedCauseLabel : t.Cause.Trim())
            .Select(g => new TicketCauseCount(g.Key, g.Count(), g.ToList()))
            .OrderByDescending(c => c.Count)
            .ToList();

        if (tickets.Count == 0)
        {
            return grouped;
        }

        var minCount = tickets.Count * OtherCauseShareThreshold;
        var top = grouped.Where(c => c.Count >= minCount).ToList();
        var tail = grouped.Where(c => c.Count < minCount).ToList();

        if (tail.Count == 0)
        {
            return top;
        }

        var otherTickets = tail.SelectMany(c => c.Tickets).ToList();
        top.Add(new TicketCauseCount(OtherCauseLabel, otherTickets.Count, otherTickets));
        return top;
    }

    /// <summary>
    /// Gruppiert Tickets nach Störungsort (Excel-Spalte "Fehlercode Ort"). Fehlt der
    /// Störungsort, zählt das Ticket als "Nicht angegeben". Anders als bei
    /// <see cref="BuildCauseBreakdown"/> gilt hier eine in den Einstellungen
    /// konfigurierbare Top-X-Grenze statt eines Prozentanteils: die
    /// <paramref name="topCount"/> häufigsten Störungsorte werden einzeln ausgewiesen,
    /// der Rest wird zu "Sonstige" zusammengefasst.
    /// </summary>
    private static List<TicketErrorLocationCount> BuildErrorLocationBreakdown(IReadOnlyList<ServiceTicket> tickets, int topCount)
    {
        var grouped = tickets
            .GroupBy(t => string.IsNullOrWhiteSpace(t.ErrorLocation) ? UnspecifiedLocationLabel : t.ErrorLocation.Trim())
            .Select(g => new TicketErrorLocationCount(g.Key, g.Count(), g.ToList()))
            .OrderByDescending(c => c.Count)
            .ToList();

        var effectiveTopCount = Math.Max(1, topCount);
        if (grouped.Count <= effectiveTopCount)
        {
            return grouped;
        }

        var top = grouped.Take(effectiveTopCount).ToList();
        var tail = grouped.Skip(effectiveTopCount).ToList();
        var otherTickets = tail.SelectMany(c => c.Tickets).ToList();
        top.Add(new TicketErrorLocationCount(OtherCauseLabel, otherTickets.Count, otherTickets));
        return top;
    }

    /// <summary>
    /// Gruppiert Tickets nach Kunde (Excel-Spalte "Adresszeile 1") für die
    /// Kundenauswertung (Tabelle "Tickets pro Kunde"). Fehlt die Adresszeile, zählt
    /// das Ticket als "Nicht angegeben". Anders als bei den Kuchendiagrammen gibt es
    /// hier keinen Sammeleintrag "Sonstige": überzählige Kunden (über
    /// <paramref name="topCount"/> hinaus) werden in der Tabelle schlicht nicht
    /// angezeigt, da "Anzahl der angezeigten Kunden" eine reine Anzeigebegrenzung ist.
    /// </summary>
    private static List<TicketCustomerCount> BuildCustomerBreakdown(IReadOnlyList<ServiceTicket> tickets, int topCount)
    {
        var grouped = tickets
            .GroupBy(t => string.IsNullOrWhiteSpace(t.AddressLine) ? UnspecifiedCustomerLabel : t.AddressLine.Trim())
            .Select(g => new TicketCustomerCount(g.Key, g.Count(), g.ToList()))
            .OrderByDescending(c => c.Count)
            .ThenBy(c => c.CustomerName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var effectiveTopCount = Math.Max(1, topCount);
        return grouped.Take(effectiveTopCount).ToList();
    }

    /// <summary>
    /// Gruppiert abgeschlossene Tickets (Excel-Spalte "Externer Status" =
    /// "Abgeschlossen") nach Bearbeitszeit (Letzte Änderung − Anlagedatum, in Tagen)
    /// in die festen Bereiche aus <see cref="ProcessingTimeBuckets"/>. Tickets mit
    /// einem anderen externen Status oder ohne "Letzte Änderung" (Spalte fehlt oder
    /// Zelle leer) werden nicht berücksichtigt, da für sie keine Bearbeitszeit
    /// bekannt ist. Eine negative Bearbeitszeit (z. B. durch fehlerhafte Daten in
    /// der Exceldatei) wird auf 0 Tage begrenzt, statt das Ticket auszuschließen.
    /// Anders als bei den übrigen Verteilungen ist die Reihenfolge der
    /// zurückgegebenen Buckets fest (aufsteigende Bearbeitszeit) statt nach
    /// Häufigkeit sortiert, und alle Buckets werden auch bei Anzahl 0 zurückgegeben,
    /// damit die X-Achse des Diagramms stets alle Bereiche zeigt.
    /// </summary>
    private static List<TicketProcessingTimeBucket> BuildProcessingTimeBreakdown(IReadOnlyList<ServiceTicket> tickets)
    {
        var closedTickets = tickets
            .Where(t => string.Equals(t.ExternalStatus?.Trim(), ClosedExternalStatus, StringComparison.OrdinalIgnoreCase))
            .Where(t => t.LastChangedAt.HasValue)
            .ToList();

        var bucketedTickets = ProcessingTimeBuckets.Select(_ => new List<ServiceTicket>()).ToList();

        foreach (var ticket in closedTickets)
        {
            var processingDays = Math.Max(0, (ticket.LastChangedAt!.Value - ticket.CreatedAt).TotalDays);
            var bucketIndex = ProcessingTimeBuckets.Length - 1;
            for (var i = 0; i < ProcessingTimeBuckets.Length; i++)
            {
                var maxDays = ProcessingTimeBuckets[i].MaxDays;
                if (maxDays is null || processingDays <= maxDays.Value)
                {
                    bucketIndex = i;
                    break;
                }
            }

            bucketedTickets[bucketIndex].Add(ticket);
        }

        return ProcessingTimeBuckets
            .Select((bucket, index) => new TicketProcessingTimeBucket(bucket.Label, bucketedTickets[index].Count, bucketedTickets[index]))
            .ToList();
    }
}
