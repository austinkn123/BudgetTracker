using BudgetTracker.Domain.Data;

namespace BudgetTracker.Tests.Data;

/// <summary>
/// User search terms are embedded in a LIKE/ILIKE pattern, so their wildcard and escape characters must be matched
/// literally rather than interpreted.
/// </summary>
public class LikePatternTests
{
    [Fact]
    public void Should_WrapTermInWildcards_When_TermHasNoSpecialCharacters()
    {
        Assert.Equal("%coffee%", LikePattern.Contains("coffee"));
    }

    [Theory]
    [InlineData("50%", @"%50\%%")]
    [InlineData("a_b", @"%a\_b%")]
    [InlineData(@"c:\temp", @"%c:\\temp%")]
    public void Should_EscapeWildcardOrEscapeCharacter_When_TermContainsIt(string term, string expected)
    {
        Assert.Equal(expected, LikePattern.Contains(term));
    }

    [Fact]
    public void Should_EscapeTheEscapeCharacterFirst_When_TermMixesSpecialCharacters()
    {
        // Escaping "\" after "%" would double the backslash that was just added in front of "%".
        Assert.Equal(@"%\\\%\_%", LikePattern.Contains(@"\%_"));
    }

    [Fact]
    public void Should_ReturnMatchAllPattern_When_TermIsEmpty()
    {
        Assert.Equal("%%", LikePattern.Contains(string.Empty));
    }

    [Fact]
    public void Should_Throw_When_TermIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => LikePattern.Contains(null!));
    }

    [Fact]
    public void Should_UseBackslashAsEscapeCharacter()
    {
        Assert.Equal(@"\", LikePattern.EscapeCharacter);
    }
}
