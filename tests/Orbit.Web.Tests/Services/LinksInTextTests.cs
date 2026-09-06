using Orbit.Web.Services;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// Finding the web addresses in something somebody wrote. The rules that matter are about what is
/// *not* linked as much as what is: a description can be written by whoever shared the thing it is on.
/// </summary>
public sealed class LinksInTextTests
{
    /// <summary>The runs as "text" for words and "text -> url" for a link, which is the whole answer in one line.</summary>
    private static string[] Runs(string? text)
        => [.. LinksInText.Split(text).Select(run => run.Url is null ? run.Text : $"{run.Text} -> {run.Url}")];

    [Fact]
    public void A_description_with_no_address_in_it_is_one_run_of_words()
        => Assert.Equal(["Bring the X-rays."], Runs("Bring the X-rays."));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Nothing_written_is_nothing_to_draw(string? nothing)
        => Assert.Empty(LinksInText.Split(nothing));

    [Fact]
    public void An_address_in_the_middle_of_a_sentence_is_its_own_run()
        => Assert.Equal(
            ["See ", "https://example.com/menu -> https://example.com/menu", " before Friday."],
            Runs("See https://example.com/menu before Friday."));

    /// <summary>People write it this way, so it is followed - with the scheme it would have been written with.</summary>
    [Fact]
    public void A_bare_www_address_is_followed_over_https()
        => Assert.Equal(["www.example.com -> https://www.example.com"], Runs("www.example.com"));

    [Fact]
    public void Plain_http_is_kept_as_it_was_written()
        => Assert.Equal(["http://example.com -> http://example.com"], Runs("http://example.com"));

    /// <summary>The full stop ends the sentence, not the address.</summary>
    [Fact]
    public void The_punctuation_after_an_address_stays_outside_it()
        => Assert.Equal(
            ["https://example.com -> https://example.com", "."],
            Runs("https://example.com."));

    [Fact]
    public void An_address_written_inside_brackets_leaves_the_closing_one_behind()
        => Assert.Equal(
            ["(", "https://example.com -> https://example.com", ")"],
            Runs("(https://example.com)"));

    /// <summary>But a bracket the address itself opened is part of it - Wikipedia writes them this way.</summary>
    [Fact]
    public void A_bracket_the_address_opened_is_kept()
        => Assert.Equal(
            ["https://example.com/wiki/Orbit_(disambiguation) -> https://example.com/wiki/Orbit_(disambiguation)"],
            Runs("https://example.com/wiki/Orbit_(disambiguation)"));

    [Fact]
    public void Two_addresses_in_one_description_are_both_found()
        => Assert.Equal(
            [
                "https://one.example -> https://one.example",
                " and ",
                "https://two.example -> https://two.example"
            ],
            Runs("https://one.example and https://two.example"));

    /// <summary>
    /// The rule the whole thing exists for. A description is written by people, and one of them may be
    /// somebody who shared a note with you - so an href is only ever http or https, and every other
    /// scheme stays words.
    /// </summary>
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html;base64,PHNjcmlwdD4=")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("file:///etc/passwd")]
    public void No_other_scheme_is_ever_followed(string dangerous)
    {
        var runs = LinksInText.Split($"Look at {dangerous} now");

        Assert.All(runs, run => Assert.Null(run.Url));
    }

    /// <summary>An address inside one of those is not a way of smuggling one out of it either.</summary>
    [Fact]
    public void A_scheme_that_hides_an_address_inside_it_is_still_not_followed()
    {
        var runs = LinksInText.Split("javascript:window.open('https://example.com')");

        var followed = runs.Where(run => run.Url is not null).ToList();
        // The https:// inside it is a real address and is linked as one; what must never happen is the
        // javascript: part becoming an href.
        Assert.All(followed, run => Assert.StartsWith("https://", run.Url!));
        Assert.DoesNotContain(followed, run => run.Url!.Contains("javascript", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The tail of an email address is not a link to its domain.</summary>
    [Fact]
    public void An_email_address_is_left_alone()
        => Assert.All(LinksInText.Split("Write to anna@www.example.com"), run => Assert.Null(run.Url));

    /// <summary>Somebody typing, not an address - a link to a host that does not exist helps nobody.</summary>
    [Theory]
    [InlineData("https://")]
    [InlineData("www.")]
    public void A_scheme_with_nothing_after_it_stays_words(string typed)
        => Assert.All(LinksInText.Split(typed), run => Assert.Null(run.Url));

    [Fact]
    public void Whether_there_is_an_address_at_all_can_be_asked_without_splitting()
    {
        Assert.True(LinksInText.HasAny("See https://example.com"));
        Assert.False(LinksInText.HasAny("See you Friday"));
        Assert.False(LinksInText.HasAny(null));
    }
}
