using Microsoft.Extensions.Logging.Abstractions;
using ServiceApp.Core.PdfParser;
using Xunit;

namespace ServiceApp.Tests.PdfParserTests;

public sealed class SwyxVisualGroupsHourlyInterpreterTests
{
    private readonly SwyxVisualGroupsHourlyInterpreter _interpreter = new();

    [Fact]
    public void Interpret_HourWithOnlyMissedCalls_ProducesOnlyMissedRecords()
    {
        var result = _interpreter.Interpret("20-07-2026 | 06:00-07:00\t3\t0\t3\t0:00 min\t0:00 min\t0 %");

        Assert.Equal(3, result.Count);
        Assert.All(result, r => Assert.False(r.Status == ServiceApp.Core.Models.CallStatus.Answered));
        Assert.All(result, r => Assert.Equal(new DateTime(2026, 7, 20, 6, 0, 0), r.Timestamp));
        Assert.All(result, r => Assert.Null(r.DurationSeconds));
    }

    [Fact]
    public void Interpret_HourWithOneAnsweredCall_UsesAverageTalkTimeAsDuration()
    {
        var result = _interpreter.Interpret("20-07-2026 | 13:00-14:00\t1\t1\t0\t2:23 min\t0:24 min\t100 %");

        var record = Assert.Single(result);
        Assert.Equal(ServiceApp.Core.Models.CallStatus.Answered, record.Status);
        Assert.Equal(new DateTime(2026, 7, 20, 13, 0, 0), record.Timestamp);
        Assert.Equal(2 * 60 + 23, record.DurationSeconds);
    }

    [Fact]
    public void Interpret_HourWithNoCalls_ProducesNoRecords()
    {
        var result = _interpreter.Interpret("20-07-2026 | 07:00-08:00\t0\t0\t0\t0:00 min\t0:00 min\t100 %");

        Assert.Empty(result);
    }

    [Fact]
    public void Interpret_MixedAnsweredAndMissed_ProducesBothInCorrectQuantities()
    {
        var result = _interpreter.Interpret("21-07-2026 | 11:00-12:00\t2\t1\t1\t21:31 min\t0:14 min\t50 %");

        Assert.Equal(2, result.Count);
        Assert.Single(result, r => r.Status == ServiceApp.Core.Models.CallStatus.Answered);
        Assert.Single(result, r => r.Status == ServiceApp.Core.Models.CallStatus.Missed);
    }

    [Fact]
    public void Interpret_UnrelatedLine_ReturnsEmpty()
    {
        var result = _interpreter.Interpret("Warteschlange: Servicelevel 1");

        Assert.Empty(result);
    }

    [Fact]
    public void Interpret_DoesNotMatch_GermanOrIsoSingleCallLines()
    {
        Assert.Empty(_interpreter.Interpret("27.07.2026    09:15:32    Angenommen    00:03:12"));
        Assert.Empty(_interpreter.Interpret("2026-07-27 09:15:32 answered 00:03:12"));
    }
}
