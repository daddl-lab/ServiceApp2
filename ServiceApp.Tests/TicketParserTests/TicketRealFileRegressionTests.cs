using Microsoft.Extensions.Logging.Abstractions;
using ServiceApp.Core.Statistics;
using ServiceApp.Core.TicketParser;
using Xunit;

namespace ServiceApp.Tests.TicketParserTests;

/// <summary>
/// Regressionstest mit einer echten, vom Kunden bereitgestellten Ticket-Exceldatei
/// (Spalten Ticketnummer, Anlagedatum, Fehlercode Ursache, u. a.). Die erwarteten Werte
/// stammen aus einer unabhängigen Analyse der Datei (siehe Anzahl Zeilen, häufigste
/// Fehlerursache, Anzahl Tickets ohne Ursache).
/// </summary>
public sealed class TicketRealFileRegressionTests
{
    private static string SampleFilePath => Path.Combine(
        AppContext.BaseDirectory, "TestData", "SampleReports", "tickets_sample.xlsx");

    [Fact]
    public void Parse_RealTicketWorkbook_MatchesKnownRowCountAndTopCause()
    {
        var parser = new ClosedXmlServiceTicketParser(NullLogger<ClosedXmlServiceTicketParser>.Instance);

        var tickets = parser.Parse(SampleFilePath);

        Assert.Equal(7965, tickets.Count);
        Assert.Equal(1297, tickets.Count(t => t.Cause is null));

        var statisticsService = new TicketStatisticsService();
        var fullRange = DateRangeFilter.CustomRange(new DateOnly(2019, 1, 1), new DateOnly(2026, 12, 31));
        var stats = statisticsService.Compute(tickets, fullRange);

        Assert.Equal(7965, stats.TotalTickets);

        // "Nicht angegeben" (leere Zellen) ist mit 1297 Tickets die größte einzelne
        // Gruppe, noch vor der häufigsten tatsächlich angegebenen Ursache.
        var topCause = stats.CauseBreakdown.First();
        Assert.Equal("Nicht angegeben", topCause.Cause);
        Assert.Equal(1297, topCause.Count);

        var topSpecifiedCause = stats.CauseBreakdown.Single(c => c.Cause == "Elektrik Bauteil Defekt");
        Assert.Equal(1029, topSpecifiedCause.Count);
    }
}
