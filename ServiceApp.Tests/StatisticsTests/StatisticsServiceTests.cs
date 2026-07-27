using ServiceApp.Core.Models;
using ServiceApp.Core.Statistics;
using Xunit;

namespace ServiceApp.Tests.StatisticsTests;

public sealed class StatisticsServiceTests
{
    private readonly StatisticsService _service = new();

    private static CallRecord Call(DateTime timestamp, bool answered, int? durationSeconds = null)
        => new("service-1", timestamp, answered ? CallStatus.Answered : CallStatus.Missed, durationSeconds, "test.pdf");

    [Fact]
    public void Compute_MixOfAnsweredAndMissedCalls_CalculatesCorrectTotalsAndPercentages()
    {
        var records = new[]
        {
            Call(new DateTime(2026, 7, 20, 9, 0, 0), answered: true),
            Call(new DateTime(2026, 7, 20, 10, 0, 0), answered: true),
            Call(new DateTime(2026, 7, 20, 11, 0, 0), answered: false),
            Call(new DateTime(2026, 7, 21, 9, 0, 0), answered: false),
        };
        var range = DateRangeFilter.CustomRange(new DateOnly(2026, 7, 20), new DateOnly(2026, 7, 21));

        var stats = _service.Compute("service-1", "Service 1", records, range);

        Assert.Equal(4, stats.TotalCalls);
        Assert.Equal(2, stats.AnsweredCalls);
        Assert.Equal(2, stats.MissedCalls);
        Assert.Equal(50.0, stats.AnsweredPercent);
        Assert.Equal(50.0, stats.MissedPercent);
    }

    [Fact]
    public void Compute_RecordsOutsideRange_AreExcluded()
    {
        var records = new[]
        {
            Call(new DateTime(2026, 7, 20, 9, 0, 0), answered: true),
            Call(new DateTime(2026, 8, 1, 9, 0, 0), answered: true) // außerhalb des Bereichs
        };
        var range = DateRangeFilter.CustomRange(new DateOnly(2026, 7, 20), new DateOnly(2026, 7, 20));

        var stats = _service.Compute("service-1", "Service 1", records, range);

        Assert.Equal(1, stats.TotalCalls);
    }

    [Fact]
    public void Compute_DailyCounts_ContainsEveryDayInRangeIncludingZeroCallDays()
    {
        var records = new[] { Call(new DateTime(2026, 7, 20, 9, 0, 0), answered: true) };
        var range = DateRangeFilter.CustomRange(new DateOnly(2026, 7, 20), new DateOnly(2026, 7, 22));

        var stats = _service.Compute("service-1", "Service 1", records, range);

        Assert.Equal(3, stats.DailyCounts.Count);
        Assert.Equal(1, stats.DailyCounts[0].TotalCalls);
        Assert.Equal(0, stats.DailyCounts[1].TotalCalls);
        Assert.Equal(0, stats.DailyCounts[2].TotalCalls);
    }

    [Fact]
    public void Compute_BestAndWorstDay_AreDeterminedFromDaysWithCallsOnly()
    {
        var records = new[]
        {
            Call(new DateTime(2026, 7, 20, 9, 0, 0), answered: true),
            Call(new DateTime(2026, 7, 20, 10, 0, 0), answered: true),
            Call(new DateTime(2026, 7, 21, 9, 0, 0), answered: true),
        };
        // 22.07. hat keine Anrufe und darf weder bester noch schwächster Tag werden.
        var range = DateRangeFilter.CustomRange(new DateOnly(2026, 7, 20), new DateOnly(2026, 7, 22));

        var stats = _service.Compute("service-1", "Service 1", records, range);

        Assert.NotNull(stats.BestDay);
        Assert.Equal(new DateOnly(2026, 7, 20), stats.BestDay!.Date);
        Assert.Equal(2, stats.BestDay.TotalCalls);

        Assert.NotNull(stats.WorstDay);
        Assert.Equal(new DateOnly(2026, 7, 21), stats.WorstDay!.Date);
        Assert.Equal(1, stats.WorstDay.TotalCalls);
    }

    [Fact]
    public void Compute_HourlyDistribution_ContainsAll24HoursAndCorrectCounts()
    {
        var records = new[]
        {
            Call(new DateTime(2026, 7, 20, 9, 0, 0), answered: true),
            Call(new DateTime(2026, 7, 20, 9, 30, 0), answered: true),
            Call(new DateTime(2026, 7, 20, 14, 0, 0), answered: false),
        };
        var range = DateRangeFilter.CustomRange(new DateOnly(2026, 7, 20), new DateOnly(2026, 7, 20));

        var stats = _service.Compute("service-1", "Service 1", records, range);

        Assert.Equal(24, stats.HourlyDistribution.Count);
        Assert.Equal(2, stats.HourlyDistribution.Single(h => h.Hour == 9).TotalCalls);
        Assert.Equal(1, stats.HourlyDistribution.Single(h => h.Hour == 14).TotalCalls);
        Assert.Equal(0, stats.HourlyDistribution.Single(h => h.Hour == 0).TotalCalls);
    }

    [Fact]
    public void Compute_NoRecords_ReturnsZeroedStatisticsWithoutThrowing()
    {
        var range = DateRangeFilter.CustomRange(new DateOnly(2026, 7, 20), new DateOnly(2026, 7, 20));

        var stats = _service.Compute("service-1", "Service 1", Array.Empty<CallRecord>(), range);

        Assert.Equal(0, stats.TotalCalls);
        Assert.Equal(0, stats.AnsweredPercent);
        Assert.Null(stats.BestDay);
        Assert.Null(stats.WorstDay);
    }

    [Fact]
    public void Compare_ReturnsBothStatisticsUnchanged()
    {
        var range = DateRangeFilter.CustomRange(new DateOnly(2026, 7, 20), new DateOnly(2026, 7, 20));
        var first = _service.Compute("service-1", "Service 1", Array.Empty<CallRecord>(), range);
        var second = _service.Compute("service-2", "Service 2", Array.Empty<CallRecord>(), range);

        var comparison = _service.Compare(first, second);

        Assert.Same(first, comparison.First);
        Assert.Same(second, comparison.Second);
    }
}
