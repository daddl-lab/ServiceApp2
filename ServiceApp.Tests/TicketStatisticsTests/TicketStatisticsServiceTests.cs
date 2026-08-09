using ServiceApp.Core.Models;
using ServiceApp.Core.Statistics;
using Xunit;

namespace ServiceApp.Tests.TicketStatisticsTests;

public sealed class TicketStatisticsServiceTests
{
    private readonly TicketStatisticsService _service = new();

    private static ServiceTicket Ticket(DateTime createdAt, string? cause, int ticketNumber = 1, string? type = null)
        => new(ticketNumber, createdAt, cause, type);

    [Fact]
    public void Compute_TicketsWithinRange_CountsCorrectly()
    {
        var tickets = new[]
        {
            Ticket(new DateTime(2026, 3, 1), "Elektrik"),
            Ticket(new DateTime(2026, 3, 2), "Mechanik"),
            Ticket(new DateTime(2026, 4, 1), "Elektrik") // außerhalb des Bereichs
        };
        var range = DateRangeFilter.CustomRange(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        var stats = _service.Compute(tickets, range);

        Assert.Equal(2, stats.TotalTickets);
    }

    [Fact]
    public void Compute_ShortRange_UsesDailyBuckets()
    {
        var tickets = new[] { Ticket(new DateTime(2026, 3, 5), "Elektrik") };
        var range = DateRangeFilter.CustomRange(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 10));

        var stats = _service.Compute(tickets, range);

        Assert.Equal(10, stats.TimeSeries.Count);
        Assert.Equal(1, stats.TimeSeries.Single(p => p.PeriodStart == new DateOnly(2026, 3, 5)).Count);
    }

    [Fact]
    public void Compute_LongRange_UsesMonthlyBuckets()
    {
        var tickets = new[]
        {
            Ticket(new DateTime(2026, 1, 15), "Elektrik"),
            Ticket(new DateTime(2026, 1, 20), "Mechanik"),
            Ticket(new DateTime(2026, 6, 3), "Elektrik")
        };
        var range = DateRangeFilter.ThisYear(new DateOnly(2026, 6, 15));

        var stats = _service.Compute(tickets, range);

        Assert.Equal(12, stats.TimeSeries.Count);
        Assert.Equal(2, stats.TimeSeries.Single(p => p.PeriodStart == new DateOnly(2026, 1, 1)).Count);
        Assert.Equal(1, stats.TimeSeries.Single(p => p.PeriodStart == new DateOnly(2026, 6, 1)).Count);
        Assert.Equal(0, stats.TimeSeries.Single(p => p.PeriodStart == new DateOnly(2026, 3, 1)).Count);
    }

    [Fact]
    public void Compute_MissingCause_GroupedAsNichtAngegeben()
    {
        var tickets = new[]
        {
            Ticket(new DateTime(2026, 3, 1), null),
            Ticket(new DateTime(2026, 3, 2), "  "),
            Ticket(new DateTime(2026, 3, 3), "Elektrik")
        };
        var range = DateRangeFilter.ThisMonth(new DateOnly(2026, 3, 15));

        var stats = _service.Compute(tickets, range);

        Assert.Equal(2, stats.CauseBreakdown.Single(c => c.Cause == "Nicht angegeben").Count);
        Assert.Equal(1, stats.CauseBreakdown.Single(c => c.Cause == "Elektrik").Count);
    }

    [Fact]
    public void Compute_ManyDistinctCauses_GroupsCausesBelowTwoPercentIntoSonstige()
    {
        // Gesamt 100 Tickets: A/B/C/D liegen bei >= 2 % (50, 30, 15, 2) und bleiben
        // einzeln, E/F/G liegen je bei 1 % und werden zu "Sonstige" zusammengefasst.
        var shares = new (string Cause, int Count)[]
        {
            ("Ursache A", 50),
            ("Ursache B", 30),
            ("Ursache C", 15),
            ("Ursache D", 2),
            ("Ursache E", 1),
            ("Ursache F", 1),
            ("Ursache G", 1)
        };
        var tickets = new List<ServiceTicket>();
        foreach (var (cause, count) in shares)
        {
            for (var i = 0; i < count; i++)
            {
                tickets.Add(Ticket(new DateTime(2026, 3, 1), cause));
            }
        }
        var range = DateRangeFilter.ThisMonth(new DateOnly(2026, 3, 15));

        var stats = _service.Compute(tickets, range);

        Assert.Equal(5, stats.CauseBreakdown.Count);
        Assert.Equal(new[] { "Ursache A", "Ursache B", "Ursache C", "Ursache D", "Sonstige" },
            stats.CauseBreakdown.Select(c => c.Cause));
        Assert.Equal(3, stats.CauseBreakdown.Last().Count);
        Assert.Equal(tickets.Count, stats.CauseBreakdown.Sum(c => c.Count));
        Assert.Equal(tickets.Count, stats.CauseBreakdown.Sum(c => c.Tickets.Count));
        Assert.Equal(stats.CauseBreakdown.Last().Count, stats.CauseBreakdown.Last().Tickets.Count);
    }

    [Fact]
    public void Compute_AllCausesAboveTwoPercent_NoSonstigeGroupCreated()
    {
        var tickets = new List<ServiceTicket>();
        for (var i = 0; i < 5; i++)
        {
            tickets.Add(Ticket(new DateTime(2026, 3, 1), $"Ursache {i}"));
        }
        var range = DateRangeFilter.ThisMonth(new DateOnly(2026, 3, 15));

        var stats = _service.Compute(tickets, range);

        Assert.Equal(5, stats.CauseBreakdown.Count);
        Assert.DoesNotContain(stats.CauseBreakdown, c => c.Cause == "Sonstige");
    }

    [Fact]
    public void Compute_CauseBreakdown_CarriesMatchingTicketsForDrillDown()
    {
        var elektrik1 = Ticket(new DateTime(2026, 3, 1), "Elektrik", ticketNumber: 101);
        var elektrik2 = Ticket(new DateTime(2026, 3, 2), "Elektrik", ticketNumber: 102);
        var mechanik = Ticket(new DateTime(2026, 3, 3), "Mechanik", ticketNumber: 103);
        var range = DateRangeFilter.ThisMonth(new DateOnly(2026, 3, 15));

        var stats = _service.Compute(new[] { elektrik1, elektrik2, mechanik }, range);

        var elektrikGroup = stats.CauseBreakdown.Single(c => c.Cause == "Elektrik");
        Assert.Equal(2, elektrikGroup.Tickets.Count);
        Assert.Contains(elektrikGroup.Tickets, t => t.TicketNumber == 101);
        Assert.Contains(elektrikGroup.Tickets, t => t.TicketNumber == 102);

        var mechanikGroup = stats.CauseBreakdown.Single(c => c.Cause == "Mechanik");
        Assert.Equal(new[] { 103 }, mechanikGroup.Tickets.Select(t => t.TicketNumber));
    }

    [Fact]
    public void Compute_NoTickets_ReturnsZeroedStatisticsWithoutThrowing()
    {
        var range = DateRangeFilter.Today(new DateOnly(2026, 3, 15));

        var stats = _service.Compute(Array.Empty<ServiceTicket>(), range);

        Assert.Equal(0, stats.TotalTickets);
        Assert.Empty(stats.CauseBreakdown);
        Assert.Single(stats.TimeSeries);
        Assert.Equal(0, stats.TimeSeries[0].Count);
    }

    [Fact]
    public void Compute_WithSelectedTypes_OnlyCountsMatchingTickets()
    {
        var tickets = new[]
        {
            Ticket(new DateTime(2026, 3, 1), "Elektrik", type: "Störung"),
            Ticket(new DateTime(2026, 3, 2), "Mechanik", type: "Anfrage"),
            Ticket(new DateTime(2026, 3, 3), "Elektrik", type: "Störung")
        };
        var range = DateRangeFilter.ThisMonth(new DateOnly(2026, 3, 15));

        var stats = _service.Compute(tickets, range, new[] { "Störung" });

        Assert.Equal(2, stats.TotalTickets);
    }

    [Fact]
    public void Compute_EmptySelectedTypes_MeansNoFilter()
    {
        var tickets = new[]
        {
            Ticket(new DateTime(2026, 3, 1), "Elektrik", type: "Störung"),
            Ticket(new DateTime(2026, 3, 2), "Mechanik", type: "Anfrage")
        };
        var range = DateRangeFilter.ThisMonth(new DateOnly(2026, 3, 15));

        var stats = _service.Compute(tickets, range, Array.Empty<string>());

        Assert.Equal(2, stats.TotalTickets);
    }

    [Fact]
    public void Compute_NullSelectedTypes_MeansNoFilter()
    {
        var tickets = new[] { Ticket(new DateTime(2026, 3, 1), "Elektrik", type: "Störung") };
        var range = DateRangeFilter.ThisMonth(new DateOnly(2026, 3, 15));

        var stats = _service.Compute(tickets, range, selectedTypes: null);

        Assert.Equal(1, stats.TotalTickets);
    }

    [Fact]
    public void Compute_MissingType_GroupedAsNichtAngegebenForFiltering()
    {
        var tickets = new[]
        {
            Ticket(new DateTime(2026, 3, 1), "Elektrik", type: null),
            Ticket(new DateTime(2026, 3, 2), "Mechanik", type: "Störung")
        };
        var range = DateRangeFilter.ThisMonth(new DateOnly(2026, 3, 15));

        var stats = _service.Compute(tickets, range, new[] { "Nicht angegeben" });

        Assert.Equal(1, stats.TotalTickets);
    }

    [Fact]
    public void GetDistinctTypes_ReturnsSortedUniqueValuesWithNichtAngegebenForMissingType()
    {
        var tickets = new[]
        {
            Ticket(new DateTime(2026, 3, 1), null, type: "Störung"),
            Ticket(new DateTime(2026, 3, 2), null, type: "Anfrage"),
            Ticket(new DateTime(2026, 3, 3), null, type: "Störung"),
            Ticket(new DateTime(2026, 3, 4), null, type: null)
        };

        var types = _service.GetDistinctTypes(tickets);

        Assert.Equal(new[] { "Anfrage", "Nicht angegeben", "Störung" }, types);
    }
}
