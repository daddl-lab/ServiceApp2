using ServiceApp.Core.FileArchive;
using ServiceApp.Core.Models;
using Xunit;

namespace ServiceApp.Tests.FileSearchTests;

public sealed class ArchivePathResolverTests
{
    private static readonly IReadOnlyList<int> DefaultLevels = ArchivePathResolver.DefaultLevelLengths;

    [Fact]
    public void Resolve_ExactNineDigitTerm_ResolvesAllThreeLevelsExactly()
    {
        var plan = ArchivePathResolver.Resolve("123456789", DefaultLevels);

        Assert.Equal(ArchiveNarrowingKind.Exact, plan.Kind);
        Assert.Equal("12", plan.Level1);
        Assert.Equal("345", plan.Level2);
        Assert.Equal("6789", plan.Level3);
        Assert.Null(plan.WildcardLevelNamePattern);
    }

    [Fact]
    public void Resolve_QuestionMarkInsideLevel3_ResolvesLevel1And2ExactlyAndBuildsWildcardForLevel3()
    {
        var plan = ArchivePathResolver.Resolve("12345?789", DefaultLevels);

        Assert.Equal(ArchiveNarrowingKind.PartialLevel3, plan.Kind);
        Assert.Equal("12", plan.Level1);
        Assert.Equal("345", plan.Level2);
        Assert.Null(plan.Level3);
        Assert.Equal("?789", plan.WildcardLevelNamePattern);
    }

    [Fact]
    public void Resolve_StarInsideLevel3_ResolvesLevel1And2ExactlyAndBuildsWildcardForLevel3()
    {
        var plan = ArchivePathResolver.Resolve("123456*", DefaultLevels);

        Assert.Equal(ArchiveNarrowingKind.PartialLevel3, plan.Kind);
        Assert.Equal("12", plan.Level1);
        Assert.Equal("345", plan.Level2);
        Assert.Equal("6*", plan.WildcardLevelNamePattern);
    }

    [Fact]
    public void Resolve_TermCoveringOnlyLevel1AndLevel2Exactly_TreatsMissingLevel3AsWildcard()
    {
        // Term is exactly 5 literal chars: fully covers level 1+2, nothing for level 3.
        var plan = ArchivePathResolver.Resolve("12345", DefaultLevels);

        Assert.Equal(ArchiveNarrowingKind.PartialLevel3, plan.Kind);
        Assert.Equal("12", plan.Level1);
        Assert.Equal("345", plan.Level2);
        Assert.Equal("*", plan.WildcardLevelNamePattern);
    }

    [Fact]
    public void Resolve_ShortTermCoveringOnlyLevel1_ResolvesLevel1AndWildcardsLevel2()
    {
        var plan = ArchivePathResolver.Resolve("12", DefaultLevels);

        Assert.Equal(ArchiveNarrowingKind.PartialLevel2, plan.Kind);
        Assert.Equal("12", plan.Level1);
        Assert.Null(plan.Level2);
        Assert.Equal("*", plan.WildcardLevelNamePattern);
    }

    [Fact]
    public void Resolve_QuestionMarkInsideLevel1_IsUnresolvable()
    {
        // A wildcard falling inside level 1 itself cannot be narrowed at all - level 1 is
        // the minimum granularity the resolver can pin down; even a single unknown
        // character there means the search must fall back to the full index/live scan.
        var plan = ArchivePathResolver.Resolve("1?45", DefaultLevels);

        Assert.Equal(ArchiveNarrowingKind.Unresolvable, plan.Kind);
    }

    [Theory]
    [InlineData("*456789")]
    [InlineData("?23456789")]
    [InlineData("")]
    [InlineData("1")]
    public void Resolve_TermsThatCannotResolveEvenLevel1_ReturnUnresolvable(string term)
    {
        var plan = string.IsNullOrEmpty(term)
            ? ArchivePathResolver.Resolve(term, DefaultLevels)
            : ArchivePathResolver.Resolve(term, DefaultLevels);

        Assert.Equal(ArchiveNarrowingKind.Unresolvable, plan.Kind);
    }

    [Theory]
    [InlineData("12/3456789")]
    [InlineData("12\\3456789")]
    [InlineData("12:3456789")]
    public void Resolve_TermWithPathInvalidCharactersInLiteralPrefix_ReturnsUnresolvable(string term)
    {
        var plan = ArchivePathResolver.Resolve(term, DefaultLevels);

        Assert.Equal(ArchiveNarrowingKind.Unresolvable, plan.Kind);
    }

    [Fact]
    public void Resolve_CombinedWildcardTerm_ResolvesLevel1ExactlyAndBuildsWildcardForLevel2()
    {
        // "12*45?789": literal prefix is "12" (2 chars), matching level 1 exactly.
        // The level-2 slice is read raw from the term at [2..5) = "*45", passed through
        // unchanged as a directory-name wildcard pattern for level 2.
        var plan = ArchivePathResolver.Resolve("12*45?789", DefaultLevels);

        Assert.Equal(ArchiveNarrowingKind.PartialLevel2, plan.Kind);
        Assert.Equal("12", plan.Level1);
        Assert.Equal("*45", plan.WildcardLevelNamePattern);
    }

    [Fact]
    public void Resolve_CustomLevelLengths_UsesConfiguredLengthsInsteadOfDefaults()
    {
        var customLevels = new[] { 3, 3, 3 };

        var plan = ArchivePathResolver.Resolve("123456789", customLevels);

        Assert.Equal(ArchiveNarrowingKind.Exact, plan.Kind);
        Assert.Equal("123", plan.Level1);
        Assert.Equal("456", plan.Level2);
        Assert.Equal("789", plan.Level3);
    }

    [Fact]
    public void Resolve_TermLongerThanAllThreeLevels_IgnoresTrailingCharactersForNarrowing()
    {
        var plan = ArchivePathResolver.Resolve("123456789_A", DefaultLevels);

        Assert.Equal(ArchiveNarrowingKind.Exact, plan.Kind);
        Assert.Equal("12", plan.Level1);
        Assert.Equal("345", plan.Level2);
        Assert.Equal("6789", plan.Level3);
    }

    [Fact]
    public void Resolve_InvalidLevelLengthsConfiguration_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ArchivePathResolver.Resolve("123456789", new[] { 2, 3 }));
    }
}
