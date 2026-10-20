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
    /// Standard-Obergrenzen (in Tagen) der Bearbeitszeit-Bereiche, falls in den
    /// Einstellungen keine oder eine leere Liste konfiguriert ist (siehe
    /// <see cref="AppSettings.TicketProcessingTimeBucketDays"/>).
    /// </summary>
    private static readonly int[] DefaultProcessingTimeBucketDays = { 1, 2, 3, 7, 14, 30, 60 };

    /// <inheritdoc />
    public TicketStatistics Compute(
        IReadOnlyList<ServiceTicket> tickets,
        DateRangeFilter range,
        IReadOnlyCollection<string>? selectedTypes = null,
        int errorLocationTopCount = 8,
        int customerTopCount = 15,
        IReadOnlyCollection<int>? processingTimeBucketDays = null)
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
            ProcessingTimeBreakdown = BuildProcessingTimeBreakdown(ticketsInRange, processingTimeBucketDays)
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
    /// in die durch <paramref name="bucketDays"/> konfigurierten Bereiche (siehe
    /// <see cref="AppSettings.TicketProcessingTimeBucketDays"/>; ungültige Werte
    /// (&lt;= 0), Duplikate werden entfernt, danach aufsteigend sortiert) plus einen
    /// automatisch ergänzten, unbegrenzten "Rest"-Bereich für alles darüber. Ist
    /// <paramref name="bucketDays"/> <c>null</c> oder leer, wird
    /// <see cref="DefaultProcessingTimeBucketDays"/> verwendet. Tickets mit einem
    /// anderen externen Status oder ohne "Letzte Änderung" (Spalte fehlt oder Zelle
    /// leer) werden nicht berücksichtigt, da für sie keine Bearbeitszeit bekannt
    /// ist. Eine negative Bearbeitszeit (z. B. durch fehlerhafte Daten in der
    /// Exceldatei) wird auf 0 Tage begrenzt, statt das Ticket auszuschließen.
    /// Anders als bei den übrigen Verteilungen ist die Reihenfolge der
    /// zurückgegebenen Buckets fest (aufsteigende Bearbeitszeit) statt nach
    /// Häufigkeit sortiert, und alle Buckets werden auch bei Anzahl 0 zurückgegeben,
    /// damit die X-Achse des Diagramms stets alle Bereiche zeigt.
    /// </summary>
    private static List<TicketProcessingTimeBucket> BuildProcessingTimeBreakdown(IReadOnlyList<ServiceTicket> tickets, IReadOnlyCollection<int>? bucketDays)
    {
        var effectiveBucketDays = (bucketDays?.Where(d => d > 0).Distinct().OrderBy(d => d).ToList() is { Count: > 0 } configured
            ? configured
            : DefaultProcessingTimeBucketDays.ToList());

        var buckets = effectiveBucketDays
            .Select(days => (Label: FormatProcessingTimeBucketLabel(days), MaxDays: (double?)days))
            .Append((Label: "Rest", MaxDays: (double?)null))
            .ToList();

        var closedTickets = tickets
            .Where(t => string.Equals(t.ExternalStatus?.Trim(), ClosedExternalStatus, StringComparison.OrdinalIgnoreCase))
            .Where(t => t.LastChangedAt.HasValue)
            .ToList();

        var bucketedTickets = buckets.Select(_ => new List<ServiceTicket>()).ToList();

        foreach (var ticket in closedTickets)
        {
            var processingDays = Math.Max(0, (ticket.LastChangedAt!.Value - ticket.CreatedAt).TotalDays);
            var bucketIndex = buckets.Count - 1;
            for (var i = 0; i < buckets.Count; i++)
            {
                var maxDays = buckets[i].MaxDays;
                if (maxDays is null || processingDays <= maxDays.Value)
                {
                    bucketIndex = i;
                    break;
                }
            }

            bucketedTickets[bucketIndex].Add(ticket);
        }

        return buckets
            .Select((bucket, index) => new TicketProcessingTimeBucket(bucket.Label, bucketedTickets[index].Count, bucketedTickets[index]))
            .ToList();
    }

    /// <summary>
    /// Erzeugt eine lesbare Bezeichnung für einen Bearbeitszeit-Bereich aus der
    /// Anzahl Tage - als volle Monate ("1 Monat", "2 Monate", ...) bzw. volle Wochen
    /// ("1 Woche", "2 Wochen", ...), wenn die Tage glatt aufgehen, sonst in Tagen
    /// ("1 Tag", "5 Tage", ...). Monate werden vor Wochen geprüft, damit z. B. 30
    /// Tage als "1 Monat" statt "4 Wochen" (unrunder Rest von 2 Tagen) angezeigt wird.
    /// </summary>
    private static string FormatProcessingTimeBucketLabel(int days)
    {
        if (days >= 30 && days % 30 == 0)
        {
            var months = days / 30;
            return months == 1 ? "1 Monat" : $"{months} Monate";
        }

        if (days >= 7 && days % 7 == 0)
        {
            var weeks = days / 7;
            return weeks == 1 ? "1 Woche" : $"{weeks} Wochen";
        }

        return days == 1 ? "1 Tag" : $"{days} Tage";
    }
}
