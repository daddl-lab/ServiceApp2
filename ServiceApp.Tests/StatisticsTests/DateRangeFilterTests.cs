using ServiceApp.Core.Statistics;
using Xunit;

namespace ServiceApp.Tests.StatisticsTests;

public sealed class DateRangeFilterTests
{
    [Fact]
    public void Today_ReturnsSingleDayRange()
    {
        var reference = new DateOnly(2026, 7, 27);
        var range = DateRangeFilter.Today(reference);

        Assert.Equal(reference, range.Start);
        Assert.Equal(reference, range.End);
        Assert.Equal(DateRangePreset.Today, range.Preset);
    }

    [Fact]
    public void ThisWeek_MondayToSunday_ContainsReferenceDate()
    {
        // 27.07.2026 ist ein Montag.
        var monday = new DateOnly(2026, 7, 27);
        var range = DateRangeFilter.ThisWeek(monday);

        Assert.Equal(monday, range.Start);
        Assert.Equal(monday.AddDays(6), range.End);
        Assert.True(range.Contains(monday.AddDays(3)));
    }

    [Fact]
    public void ThisWeek_ReferenceMidWeek_StartsOnMonday()
    {
        // 29.07.2026 ist ein Mittwoch.
        var wednesday = new DateOnly(2026, 7, 29);
        var range = DateRangeFilter.ThisWeek(wednesday);

        Assert.Equal(new DateOnly(2026, 7, 27), range.Start);
        Assert.Equal(new DateOnly(2026, 8, 2), range.End);
    }

    [Fact]
    public void ThisMonth_ReturnsFirstToLastDayOfMonth()
    {
        var reference = new DateOnly(2026, 2, 15);
        var range = DateRangeFilter.ThisMonth(reference);

        Assert.Equal(new DateOnly(2026, 2, 1), range.Start);
        Assert.Equal(new DateOnly(2026, 2, 28), range.End);
    }

    [Fact]
    public void ThisYear_ReturnsJanuaryFirstToDecember31st()
    {
        var reference = new DateOnly(2026, 5, 10);
        var range = DateRangeFilter.ThisYear(reference);

        Assert.Equal(new DateOnly(2026, 1, 1), range.Start);
        Assert.Equal(new DateOnly(2026, 12, 31), range.End);
        Assert.Equal(DateRangePreset.ThisYear, range.Preset);
    }

    [Fact]
    public void CustomRange_StartAfterEnd_SwapsDatesAutomatically()
    {
        var range = DateRangeFilter.CustomRange(new DateOnly(2026, 7, 25), new DateOnly(2026, 7, 20));

        Assert.Equal(new DateOnly(2026, 7, 20), range.Start);
        Assert.Equal(new DateOnly(2026, 7, 25), range.End);
    }

    [Fact]
    public void EnumerateDays_ReturnsAllDaysInclusive()
    {
        var range = DateRangeFilter.CustomRange(new DateOnly(2026, 7, 20), new DateOnly(2026, 7, 22));

        Assert.Equal(
            new[] { new DateOnly(2026, 7, 20), new DateOnly(2026, 7, 21), new DateOnly(2026, 7, 22) },
            range.EnumerateDays());
    }
}
