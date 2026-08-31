using Orbit.Contracts.Suggestions;
using Orbit.Core.Suggestions;
using Orbit.Mobile.Screens.Suggestions;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// The names already in this account, offered under the field being typed into - the phone's half of
/// what Orbit.Web puts under the same four fields.
///
/// Every test here waits rather than asserting straight away, because the lookup is deliberately not
/// awaited: it hangs off a property setter, and a field that waited for the network between keystrokes
/// would be unusable. Waiting for the answer is what a reader does too.
/// </summary>
public sealed class NameSuggestionsTests
{
    [Fact]
    public async Task Names_that_match_what_is_being_typed_are_offered()
    {
        using var server = new FakeSuggestionsServer();
        server.Names.Add(new NameSuggestionDto("Flour, wheat", 0.4));
        server.Names.Add(new NameSuggestionDto("Sugar", 0.1));
        var suggestions = Suggestions.Offering(server);
        suggestions.Offers(NameSuggestionKind.InventoryItemName);

        suggestions.ShowFor("Flo");

        await WaitUntil(() => suggestions.Names.Count > 0);
        Assert.Equal(["Flour, wheat"], suggestions.Names);
        Assert.Equal(string.Empty, suggestions.DuplicateWarning);
    }

    /// <summary>
    /// Close enough to be the same thing spelled differently, which is a duplicate about to be created
    /// rather than a completion to offer - so it is said out loud instead of left to be spotted.
    /// </summary>
    [Fact]
    public async Task A_name_close_enough_to_be_the_same_thing_is_called_out()
    {
        using var server = new FakeSuggestionsServer();
        server.Names.Add(new NameSuggestionDto("Flour, wheat", 0.8));
        var suggestions = Suggestions.Offering(server);

        suggestions.ShowFor("Flour");

        await WaitUntil(() => suggestions.DuplicateWarning.Length > 0);
        Assert.Contains("Flour, wheat", suggestions.DuplicateWarning);
        Assert.True(suggestions.HasDuplicateWarning);
    }

    /// <summary>
    /// Suggestions are about what somebody is typing, not about what is already saved: opening an item
    /// to change its expiry date must not offer completions of its own name, nor warn that it is a
    /// duplicate of itself.
    /// </summary>
    [Fact]
    public async Task The_value_a_field_opens_on_is_not_looked_up()
    {
        using var server = new FakeSuggestionsServer();
        server.Names.Add(new NameSuggestionDto("Flour, wheat", 0.9));
        var suggestions = Suggestions.Offering(server);

        suggestions.StartsAt("Flour, wheat");
        suggestions.ShowFor("Flour, wheat");

        await Task.Delay(SettleTime);
        Assert.Equal(0, server.Lookups);
        Assert.Empty(suggestions.Names);
    }

    /// <summary>Below two characters everything looks similar to everything - the rule the server applies too.</summary>
    [Fact]
    public async Task Nothing_is_looked_up_for_a_single_letter()
    {
        using var server = new FakeSuggestionsServer();
        var suggestions = Suggestions.Offering(server);

        suggestions.ShowFor("F");

        await Task.Delay(SettleTime);
        Assert.Equal(0, server.Lookups);
    }

    /// <summary>
    /// One lookup for a word rather than one per keystroke, which is the whole point of waiting for the
    /// typing to stop.
    /// </summary>
    [Fact]
    public async Task Typing_a_word_costs_one_lookup()
    {
        using var server = new FakeSuggestionsServer();
        var suggestions = Suggestions.Offering(server);

        foreach (var typed in new[] { "Fl", "Flo", "Flou", "Flour" })
        {
            suggestions.ShowFor(typed);
        }

        await Task.Delay(SettleTime);
        Assert.Equal(1, server.Lookups);
        Assert.Equal("Flour", server.LastQuery);
    }

    [Fact]
    public async Task Choosing_a_name_hands_it_over_and_clears_what_was_on_offer()
    {
        using var server = new FakeSuggestionsServer();
        server.Names.Add(new NameSuggestionDto("Flour, wheat", 0.4));
        var suggestions = Suggestions.Offering(server);
        suggestions.ShowFor("Flo");
        await WaitUntil(() => suggestions.Names.Count > 0);

        string? taken = null;
        suggestions.Takes = name => taken = name;
        suggestions.ChooseCommand.Execute("Flour, wheat");

        Assert.Equal("Flour, wheat", taken);
        Assert.Empty(suggestions.Names);
        Assert.False(suggestions.HasAny);
    }

    /// <summary>
    /// A phone with no connection has one every few minutes. A field that stopped suggesting is fine; a
    /// field that threw while somebody was typing into it is not.
    /// </summary>
    [Fact]
    public async Task A_lookup_that_cannot_be_made_offers_nothing_rather_than_failing()
    {
        using var server = new FakeSuggestionsServer { IsUnreachable = true };
        var suggestions = Suggestions.Offering(server);

        suggestions.ShowFor("Flour");

        await Task.Delay(SettleTime);
        Assert.Empty(suggestions.Names);
        Assert.Equal(string.Empty, suggestions.DuplicateWarning);
    }

    /// <summary>Comfortably past the 150ms the lookup waits for the typing to stop.</summary>
    private static readonly TimeSpan SettleTime = TimeSpan.FromMilliseconds(600);

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 40 && !condition(); attempt++)
        {
            await Task.Delay(50);
        }

        Assert.True(condition(), "The suggestions never arrived.");
    }
}
