using Microsoft.Extensions.Logging.Abstractions;
using ServiceApp.Core.PdfParser;
using Xunit;

namespace ServiceApp.Tests.PdfParserTests;

/// <summary>
/// Regressionstest mit einem echten, vom Kunden bereitgestellten Swyx-VisualGroups-
/// Wochenbericht. Die erwarteten Summen (35 Anrufe gesamt, 6 angenommen, 29 verpasst
/// für den Zeitraum 20.-26.07.2026) stammen aus der im PDF selbst ausgewiesenen
/// Kopfzeilen-Zusammenfassung.
/// </summary>
public sealed class SwyxRealPdfRegressionTests
{
    private static string SampleFilePath => Path.Combine(
        AppContext.BaseDirectory, "TestData", "SampleReports", "swyx_servicelevel1_week.pdf");

    [Fact]
    public void Parse_RealSwyxWeeklyReport_MatchesHeaderSummaryCounts()
    {
        var parser = new PdfPigReportParser(
            new IReportLineInterpreter[]
            {
                new GermanTableLineInterpreter(),
                new IsoTableLineInterpreter(),
                new SwyxVisualGroupsHourlyInterpreter()
            },
            NullLogger<PdfPigReportParser>.Instance);

        var records = parser.Parse(SampleFilePath, "service-1");

        Assert.Equal(35, records.Count);
        Assert.Equal(6, records.Count(r => r.IsAnswered));
        Assert.Equal(29, records.Count(r => !r.IsAnswered));
        Assert.All(records, r => Assert.InRange(r.Timestamp.Date, new DateTime(2026, 7, 20), new DateTime(2026, 7, 26)));
    }
}
