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
    public void Compute_ManyDistinctCauses_GroupsTailIntoSonstige()
    {
        var tickets = new List<ServiceTicket>();
        for (var i = 0; i < 12; i++)
        {
            // Je Ursache eine absteigende Häufigkeit (12, 11, 10, ...), damit die
            // Sortierung eindeutig ist.
            for (var count = 0; count < 12 - i; count++)
            {
                tickets.Add(Ticket(new DateTime(2026, 3, 1), $"Ursache {i}"));
            }
        }
        var range = DateRangeFilter.ThisMonth(new DateOnly(2026, 3, 15));

        var stats = _service.Compute(tickets, range);

        Assert.Equal(9, stats.CauseBreakdown.Count);
        Assert.Equal("Sonstige", stats.CauseBreakdown.Last().Cause);
        Assert.Equal(tickets.Count, stats.CauseBreakdown.Sum(c => c.Count));
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
