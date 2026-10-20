using ServiceApp.Core.Models;
using Xunit;

namespace ServiceApp.Tests.ModelTests;

public sealed class CallRecordTests
{
    [Fact]
    public void IsAnswered_StatusAnswered_ReturnsTrue()
    {
        var record = new CallRecord("service-1", DateTime.Now, CallStatus.Answered, 60, "test.pdf");

        Assert.True(record.IsAnswered);
    }

    [Fact]
    public void IsAnswered_StatusMissed_ReturnsFalse()
    {
        var record = new CallRecord("service-1", DateTime.Now, CallStatus.Missed, null, "test.pdf");

        Assert.False(record.IsAnswered);
    }
}

public sealed class DailyCallCountTests
{
    [Fact]
    public void AnswerRatePercent_MixedCalls_CalculatesCorrectPercentage()
    {
        var day = new DailyCallCount(new DateOnly(2026, 7, 27), TotalCalls: 4, AnsweredCalls: 3, MissedCalls: 1);

        Assert.Equal(75.0, day.AnswerRatePercent);
    }

    [Fact]
    public void AnswerRatePercent_NoCalls_ReturnsZeroWithoutDivisionByZero()
    {
        var day = new DailyCallCount(new DateOnly(2026, 7, 27), TotalCalls: 0, AnsweredCalls: 0, MissedCalls: 0);

        Assert.Equal(0, day.AnswerRatePercent);
    }
}

public sealed class AppSettingsTests
{
    [Fact]
    public void DefaultConstructor_CreatesTwoServiceNumbersWithEmptyPaths()
    {
        var settings = new AppSettings();

        Assert.Equal(2, settings.ServiceNumbers.Count);
        Assert.All(settings.ServiceNumbers, s => Assert.Equal(string.Empty, s.PdfFolderPath));
    }

    [Fact]
    public void DefaultConstructor_TicketErrorLocationTopCount_DefaultsToEight()
    {
        var settings = new AppSettings();

        Assert.Equal(8, settings.TicketErrorLocationTopCount);
    }

    [Fact]
    public void DefaultConstructor_TicketCustomerTopCount_DefaultsToFifteen()
    {
        var settings = new AppSettings();

        Assert.Equal(15, settings.TicketCustomerTopCount);
    }

    [Fact]
    public void DefaultConstructor_TicketProcessingTimeBucketDays_DefaultsToStandardBuckets()
    {
        var settings = new AppSettings();

        Assert.Equal(new[] { 1, 2, 3, 7, 14, 30, 60 }, settings.TicketProcessingTimeBucketDays);
    }
}
