using ServiceApp.Core.Models;
using ServiceApp.Core.Statistics;
using Xunit;

namespace ServiceApp.Tests.TicketStatisticsTests;

public sealed class TicketStatisticsServiceTests
{
    private readonly TicketStatisticsService _service = new();

    private static ServiceTicket Ticket(DateTime createdAt, string? cause, int ticketNumber = 1, string? type = null, string? errorLocation = null, string? addressLine = null)
        => new(ticketNumber, createdAt, cause, type, AddressLine: addressLine, ErrorLocation: errorLocation);

    private static ServiceTicket ClosedTicket(DateTime createdAt, DateTime lastChangedAt, int ticketNumber = 1)
        => new(ticketNumber, createdAt, Cause: null, Type: null, ExternalStatus: "Abgeschlossen", LastChangedAt: lastChangedAt);

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

    [Fact]
    public void Compute_ErrorLocationBreakdown_KeepsTopXIndividualAndGroupsRestIntoSonstige()
    {
        var tickets = new List<ServiceTicket>();
        for (var i = 0; i < 5; i++)
        {
            // Absteigende Häufigkeit (5, 4, 3, 2, 1), damit die Sortierung eindeutig ist.
            for (var count = 0; count < 5 - i; count++)
            {
                tickets.Add(Ticket(new DateTime(2026, 3, 1), null, errorLocation: $"Ort {i}"));
            }
        }
        var range = DateRangeFilter.ThisMonth(new DateOnly(2026, 3, 15));

        var stats = _service.Compute(tickets, range, errorLocationTopCount: 3);

        Assert.Equal(4, stats.ErrorLocationBreakdown.Count);
        Assert.Equal(new[] { "Ort 0", "Ort 1", "Ort 2", "Sonstige" }, stats.ErrorLocationBreakdown.Select(c => c.Location));
        Assert.Equal(3, stats.ErrorLocationBreakdown.Last().Count); // Ort 3 (2) + Ort 4 (1)
        Assert.Equal(tickets.Count, stats.ErrorLocationBreakdown.Sum(c => c.Count));
        Assert.Equal(tickets.Count, stats.ErrorLocationBreakdown.Sum(c => c.Tickets.Count));
    }

    [Fact]
    public void Compute_ErrorLocationBreakdown_FewerLocationsThanTopCount_NoSonstigeGroupCreated()
    {
        var tickets = new[]
        {
            Ticket(new DateTime(2026, 3, 1), null, errorLocation: "Ort A"),
            Ticket(new DateTime(2026, 3, 2), null, errorLocation: "Ort B")
        };
        var range = DateRangeFilter.ThisMonth(new DateOnly(2026, 3, 15));

        var stats = _service.Compute(tickets, range, errorLocationTopCount: 8);

        Assert.Equal(2, stats.ErrorLocationBreakdown.Count);
        Assert.DoesNotContain(stats.ErrorLocationBreakdown, c => c.Location == "Sonstige");
    }

    [Fact]
    public void Compute_ErrorLocationBreakdown_MissingLocation_GroupedAsNichtAngegeben()
    {
        var tickets = new[]
        {
            Ticket(new DateTime(2026, 3, 1), null, errorLocation: null),
            Ticket(new DateTime(2026, 3, 2), null, errorLocation: "  "),
            Ticket(new DateTime(2026, 3, 3), null, errorLocation: "Ort A")
        };
        var range = DateRangeFilter.ThisMonth(new DateOnly(2026, 3, 15));

        var stats = _service.Compute(tickets, range);

        Assert.Equal(2, stats.ErrorLocationBreakdown.Single(c => c.Location == "Nicht angegeben").Count);
        Assert.Equal(1, stats.ErrorLocationBreakdown.Single(c => c.Location == "Ort A").Count);
    }

    [Fact]
    public void Compute_ErrorLocationTopCountLessThanOne_TreatedAsOne()
    {
        var tickets = new[]
        {
            Ticket(new DateTime(2026, 3, 1), null, errorLocation: "Ort A"),
            Ticket(new DateTime(2026, 3, 2), null, errorLocation: "Ort A"),
            Ticket(new DateTime(2026, 3, 3), null, errorLocation: "Ort B")
        };
        var range = DateRangeFilter.ThisMonth(new DateOnly(2026, 3, 15));

        var stats = _service.Compute(tickets, range, errorLocationTopCount: 0);

        Assert.Equal(new[] { "Ort A", "Sonstige" }, stats.ErrorLocationBreakdown.Select(c => c.Location));
    }

    [Fact]
    public void Compute_CustomerBreakdown_KeepsTopXWithoutSonstigeGroup()
    {
        var tickets = new List<ServiceTicket>();
        for (var i = 0; i < 5; i++)
        {
            // Absteigende Häufigkeit (5, 4, 3, 2, 1), damit die Sortierung eindeutig ist.
            for (var count = 0; count < 5 - i; count++)
            {
                tickets.Add(Ticket(new DateTime(2026, 3, 1), null, addressLine: $"Kunde {i}"));
            }
        }
        var range = DateRangeFilter.ThisMonth(new DateOnly(2026, 3, 15));

        var stats = _service.Compute(tickets, range, customerTopCount: 3);

        Assert.Equal(3, stats.CustomerBreakdown.Count);
        Assert.Equal(new[] { "Kunde 0", "Kunde 1", "Kunde 2" }, stats.CustomerBreakdown.Select(c => c.CustomerName));
        Assert.DoesNotContain(stats.CustomerBreakdown, c => c.CustomerName == "Sonstige");
        Assert.Equal(5, stats.CustomerBreakdown[0].Count);
    }

    [Fact]
    public void Compute_CustomerBreakdown_FewerCustomersThanTopCount_ReturnsAll()
    {
        var tickets = new[]
        {
            Ticket(new DateTime(2026, 3, 1), null, addressLine: "Kunde A"),
            Ticket(new DateTime(2026, 3, 2), null, addressLine: "Kunde B")
        };
        var range = DateRangeFilter.ThisMonth(new DateOnly(2026, 3, 15));

        var stats = _service.Compute(tickets, range, customerTopCount: 15);

        Assert.Equal(2, stats.CustomerBreakdown.Count);
    }

    [Fact]
    public void Compute_CustomerBreakdown_MissingAddressLine_GroupedAsNichtAngegeben()
    {
        var tickets = new[]
        {
            Ticket(new DateTime(2026, 3, 1), null, addressLine: null),
            Ticket(new DateTime(2026, 3, 2), null, addressLine: "  "),
            Ticket(new DateTime(2026, 3, 3), null, addressLine: "Kunde A")
        };
        var range = DateRangeFilter.ThisMonth(new DateOnly(2026, 3, 15));

        var stats = _service.Compute(tickets, range);

        Assert.Equal(2, stats.CustomerBreakdown.Single(c => c.CustomerName == "Nicht angegeben").Count);
        Assert.Equal(1, stats.CustomerBreakdown.Single(c => c.CustomerName == "Kunde A").Count);
    }

    [Fact]
    public void Compute_CustomerTopCountLessThanOne_TreatedAsOne()
    {
        var tickets = new[]
        {
            Ticket(new DateTime(2026, 3, 1), null, addressLine: "Kunde A"),
            Ticket(new DateTime(2026, 3, 2), null, addressLine: "Kunde A"),
            Ticket(new DateTime(2026, 3, 3), null, addressLine: "Kunde B")
        };
        var range = DateRangeFilter.ThisMonth(new DateOnly(2026, 3, 15));

        var stats = _service.Compute(tickets, range, customerTopCount: 0);

        Assert.Equal(new[] { "Kunde A" }, stats.CustomerBreakdown.Select(c => c.CustomerName));
    }

    [Fact]
    public void Compute_CustomerBreakdown_CarriesMatchingTickets()
    {
        var tickets = new[]
        {
            Ticket(new DateTime(2026, 3, 1), null, ticketNumber: 101, addressLine: "Kunde A"),
            Ticket(new DateTime(2026, 3, 2), null, ticketNumber: 102, addressLine: "Kunde A")
        };
        var range = DateRangeFilter.ThisMonth(new DateOnly(2026, 3, 15));

        var stats = _service.Compute(tickets, range);

        var customerA = stats.CustomerBreakdown.Single(c => c.CustomerName == "Kunde A");
        Assert.Equal(new[] { 101, 102 }, customerA.Tickets.Select(t => t.TicketNumber));
    }

    [Fact]
    public void Compute_ProcessingTimeBreakdown_AlwaysReturnsAllEightBucketsInFixedOrder()
    {
        var range = DateRangeFilter.ThisMonth(new DateOnly(2026, 3, 15));

        var stats = _service.Compute(Array.Empty<ServiceTicket>(), range);

        Assert.Equal(
            new[] { "1 Tag", "2 Tage", "3 Tage", "1 Woche", "2 Wochen", "1 Monat", "2 Monate", "Rest" },
            stats.ProcessingTimeBreakdown.Select(b => b.Label));
        Assert.All(stats.ProcessingTimeBreakdown, b => Assert.Equal(0, b.Count));
    }

    [Fact]
    public void Compute_ProcessingTimeBreakdown_AssignsTicketsToCorrectBucketByDays()
    {
        var created = new DateTime(2026, 3, 1);
        var tickets = new[]
        {
            ClosedTicket(created, created.AddHours(12), ticketNumber: 1),   // 0.5 Tage -> 1 Tag
            ClosedTicket(created, created.AddDays(1.5), ticketNumber: 2),   // 1.5 Tage -> 2 Tage
            ClosedTicket(created, created.AddDays(2.5), ticketNumber: 3),   // 2.5 Tage -> 3 Tage
            ClosedTicket(created, created.AddDays(5), ticketNumber: 4),     // 5 Tage -> 1 Woche
            ClosedTicket(created, created.AddDays(10), ticketNumber: 5),    // 10 Tage -> 2 Wochen
            ClosedTicket(created, created.AddDays(20), ticketNumber: 6),    // 20 Tage -> 1 Monat
            ClosedTicket(created, created.AddDays(45), ticketNumber: 7),    // 45 Tage -> 2 Monate
            ClosedTicket(created, created.AddDays(90), ticketNumber: 8)     // 90 Tage -> Rest
        };
        var range = DateRangeFilter.ThisMonth(new DateOnly(2026, 3, 15));

        var stats = _service.Compute(tickets, range);

        Assert.All(stats.ProcessingTimeBreakdown, b => Assert.Equal(1, b.Count));
        Assert.Equal(1, stats.ProcessingTimeBreakdown.Single(b => b.Label == "1 Tag").Tickets.Single().TicketNumber);
        Assert.Equal(8, stats.ProcessingTimeBreakdown.Single(b => b.Label == "Rest").Tickets.Single().TicketNumber);
    }

    [Fact]
    public void Compute_ProcessingTimeBreakdown_ExcludesTicketsNotClosed()
    {
        var created = new DateTime(2026, 3, 1);
        var tickets = new[]
        {
            ClosedTicket(created, created.AddDays(1)),
            new ServiceTicket(2, created, Cause: null, Type: null, ExternalStatus: "In Bearbeitung", LastChangedAt: created.AddDays(1))
        };
        var range = DateRangeFilter.ThisMonth(new DateOnly(2026, 3, 15));

        var stats = _service.Compute(tickets, range);

        Assert.Equal(1, stats.ProcessingTimeBreakdown.Sum(b => b.Count));
    }

    [Fact]
    public void Compute_ProcessingTimeBreakdown_ExcludesTicketsWithoutLastChangedAt()
    {
        var created = new DateTime(2026, 3, 1);
        var tickets = new[]
        {
            new ServiceTicket(1, created, Cause: null, Type: null, ExternalStatus: "Abgeschlossen", LastChangedAt: null)
        };
        var range = DateRangeFilter.ThisMonth(new DateOnly(2026, 3, 15));

        var stats = _service.Compute(tickets, range);

        Assert.Equal(0, stats.ProcessingTimeBreakdown.Sum(b => b.Count));
    }

    [Fact]
    public void Compute_ProcessingTimeBreakdown_NegativeProcessingTime_ClampedIntoFirstBucket()
    {
        var created = new DateTime(2026, 3, 10);
        var tickets = new[]
        {
            // Letzte Änderung liegt vor dem Anlagedatum - fehlerhafte Daten, sollen
            // aber nicht zum Ausschluss des Tickets führen.
            ClosedTicket(created, created.AddDays(-5))
        };
        var range = DateRangeFilter.ThisMonth(new DateOnly(2026, 3, 15));

        var stats = _service.Compute(tickets, range);

        Assert.Equal(1, stats.ProcessingTimeBreakdown.Single(b => b.Label == "1 Tag").Count);
    }
}
