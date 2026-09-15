using Orbit.Core.Text;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// Matching words the way somebody searching means them - see Orbit.Core.Text.LooseText. Kept beside
/// LinksInTextTests, which covers the other half of that namespace and is read the same way: the rules
/// are Orbit.Core's, and this is where the web's own searches are pinned down against them.
///
/// The case that matters most for Orbit is Polish, where every marked letter is one a phone keyboard
/// makes somebody hold a key for - so a search box that insists on the marks is one that finds nothing
/// until the third attempt.
/// </summary>
public sealed class LooseTextTests
{
    [Theory]
    [InlineData("Żółw", "zolw")]
    [InlineData("Żółw", "ZOLW")]
    [InlineData("zolw", "Żółw")]
    [InlineData("Gęś", "ges")]
    [InlineData("Łódź", "lodz")]
    [InlineData("Ćma", "cma")]
    [InlineData("Zaświadczenie", "zaswiadczenie")]
    public void The_marks_over_a_letter_do_not_have_to_be_typed(string written, string typed)
        => Assert.True(LooseText.Holds(written, typed));

    /// <summary>
    /// Both directions, which is the half that a search which only strips its own box would miss: a
    /// reader who does type the marks still finds a row somebody else wrote without them.
    /// </summary>
    [Fact]
    public void A_search_written_with_marks_finds_a_word_written_without_them()
        => Assert.True(LooseText.Holds("Zurek na sniadanie", "żurek"));

    /// <summary>Anywhere in the words, as every search box in Orbit matches - not only at the start.</summary>
    [Fact]
    public void The_words_are_matched_anywhere_in_the_text()
        => Assert.True(LooseText.Holds("Mąka, pszenna", "pszenna"));

    /// <summary>An empty box is not a search, so everything is still there to read.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Nothing_typed_holds_everything(string? typed)
        => Assert.True(LooseText.Holds("Żółw", typed));

    [Fact]
    public void A_word_that_is_not_there_is_still_not_there()
        => Assert.False(LooseText.Holds("Żółw", "kaczka"));

    /// <summary>
    /// The letters that are not a plain letter with a mark on it - Polish "ł" is its own letter, and
    /// taking a string apart into letters and marks leaves it alone. Somebody searching types "l".
    /// </summary>
    [Theory]
    [InlineData("Łąka", "laka")]
    [InlineData("Michał", "michal")]
    [InlineData("Wrocław", "wroclaw")]
    public void A_letter_drawn_with_a_stroke_is_found_by_its_plain_one(string written, string typed)
        => Assert.True(LooseText.Holds(written, typed));

    [Theory]
    [InlineData("Żółw", "zolw")]
    [InlineData("Michał", "MICHAL")]
    [InlineData("gęś", "Ges")]
    public void Two_spellings_of_the_same_word_are_the_same_word(string written, string other)
        => Assert.True(LooseText.Same(written, other));

    [Fact]
    public void Two_different_words_are_not_the_same_word()
        => Assert.False(LooseText.Same("Żółw", "Żółwie"));

    /// <summary>
    /// Folding answers something a reader could still recognise - it takes the marks off and changes
    /// nothing else, case included. That is what lets the comparison above decide about case instead.
    /// </summary>
    [Fact]
    public void Folding_leaves_the_word_readable()
        => Assert.Equal("Zolw", LooseText.Folded("Żółw"));

    [Fact]
    public void Folding_nothing_answers_nothing()
    {
        Assert.Equal(string.Empty, LooseText.Folded(null));
        Assert.Equal(string.Empty, LooseText.Folded(string.Empty));
    }

    /// <summary>Text with no marks in it is its own folded form - the case nearly every English word is.</summary>
    [Fact]
    public void Plain_words_are_left_exactly_as_they_are()
        => Assert.Equal("Flour, wheat", LooseText.Folded("Flour, wheat"));
}
