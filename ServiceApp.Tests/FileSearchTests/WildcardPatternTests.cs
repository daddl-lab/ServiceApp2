using ServiceApp.Core.Exceptions;
using ServiceApp.Core.FileArchive;
using Xunit;

namespace ServiceApp.Tests.FileSearchTests;

public sealed class WildcardPatternTests
{
    [Fact]
    public void IsMatch_NoWildcards_MatchesOnlyExactValue()
    {
        var pattern = WildcardPattern.Compile("123456789");

        Assert.True(pattern.IsMatch("123456789"));
        Assert.False(pattern.IsMatch("1123456789"));
        Assert.False(pattern.IsMatch("12345678"));
        Assert.False(pattern.IsMatch("123456789_A"));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("12345")]
    [InlineData("123456789")]
    [InlineData("123_A")]
    [InlineData("123-ABC")]
    public void IsMatch_TrailingStar_MatchesEverythingStartingWithPrefix(string value)
    {
        var pattern = WildcardPattern.Compile("123*");

        Assert.True(pattern.IsMatch(value));
    }

    [Fact]
    public void IsMatch_TrailingStar_DoesNotMatchValueWithoutPrefix()
    {
        var pattern = WildcardPattern.Compile("123*");

        Assert.False(pattern.IsMatch("A123"));
        Assert.False(pattern.IsMatch("12"));
    }

    [Fact]
    public void IsMatch_LeadingStar_MatchesEverythingEndingWithSuffix()
    {
        var pattern = WildcardPattern.Compile("*789");

        Assert.True(pattern.IsMatch("789"));
        Assert.True(pattern.IsMatch("123456789"));
        Assert.False(pattern.IsMatch("789123"));
    }

    [Fact]
    public void IsMatch_StarOnBothSides_BehavesAsContainsSearch()
    {
        var pattern = WildcardPattern.Compile("*456*");

        Assert.True(pattern.IsMatch("123456789"));
        Assert.True(pattern.IsMatch("456"));
        Assert.False(pattern.IsMatch("123789"));
    }

    [Theory]
    [InlineData("123456789")]
    [InlineData("12345A789")]
    [InlineData("12345-789")]
    public void IsMatch_SingleQuestionMark_MatchesExactlyOneArbitraryCharacter(string value)
    {
        var pattern = WildcardPattern.Compile("12345?789");

        Assert.True(pattern.IsMatch(value));
    }

    [Theory]
    [InlineData("12345789")]
    [InlineData("12345AB789")]
    public void IsMatch_SingleQuestionMark_DoesNotMatchWrongCharacterCount(string value)
    {
        var pattern = WildcardPattern.Compile("12345?789");

        Assert.False(pattern.IsMatch(value));
    }

    [Fact]
    public void IsMatch_CombinationOfStarAndQuestionMark_MatchesAccordingToBothRules()
    {
        var pattern = WildcardPattern.Compile("12*45?789");

        Assert.True(pattern.IsMatch("1245A789"));
        Assert.True(pattern.IsMatch("12XYZ45A789"));
        Assert.False(pattern.IsMatch("1245789"));
        Assert.False(pattern.IsMatch("12XYZ45AB789"));
    }

    [Fact]
    public void IsMatch_DefaultCaseSensitivity_IsCaseInsensitive()
    {
        var pattern = WildcardPattern.Compile("ABC*");

        Assert.True(pattern.IsMatch("abc123"));
        Assert.True(pattern.IsMatch("ABC123"));
        Assert.True(pattern.IsMatch("AbC123"));
    }

    [Fact]
    public void IsMatch_CaseSensitiveExplicitlyRequested_DistinguishesCase()
    {
        var pattern = WildcardPattern.Compile("ABC*", caseSensitive: true);

        Assert.True(pattern.IsMatch("ABC123"));
        Assert.False(pattern.IsMatch("abc123"));
    }

    [Fact]
    public void IsMatch_SpecialRegexCharactersInSearchTerm_AreTreatedAsLiteralText()
    {
        var pattern = WildcardPattern.Compile("A.B(C)+");

        Assert.True(pattern.IsMatch("A.B(C)+"));
        Assert.False(pattern.IsMatch("AxB(C)+"));
        Assert.False(pattern.IsMatch("A.BCCC"));
    }

    [Fact]
    public void ContainsWildcards_TermWithoutWildcards_IsFalse()
    {
        var pattern = WildcardPattern.Compile("123456789");

        Assert.False(pattern.ContainsWildcards);
    }

    [Theory]
    [InlineData("123*")]
    [InlineData("12345?789")]
    [InlineData("*789")]
    public void ContainsWildcards_TermWithWildcards_IsTrue(string term)
    {
        var pattern = WildcardPattern.Compile(term);

        Assert.True(pattern.ContainsWildcards);
    }

    [Fact]
    public void Compile_EmptySearchTerm_ThrowsInvalidSearchTermException()
    {
        Assert.Throws<InvalidSearchTermException>(() => WildcardPattern.Compile(string.Empty));
    }

    [Fact]
    public void Compile_WhitespaceOnlySearchTerm_ThrowsInvalidSearchTermException()
    {
        Assert.Throws<InvalidSearchTermException>(() => WildcardPattern.Compile("   "));
    }

    [Fact]
    public void IsMatch_GermanUmlautsInSearchTermAndValue_MatchCaseInsensitively()
    {
        var pattern = WildcardPattern.Compile("Prüfplan*");

        Assert.True(pattern.IsMatch("prüfplan_v2"));
        Assert.True(pattern.IsMatch("PRÜFPLAN_V2"));
    }
}
