using System.Net;
using System.Text;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Orbit.Web.Components;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Components;

/// <summary>
/// The control a short vocabulary is read and chosen in - see ValueBrowser.razor. What it holds is
/// written along the closed field; what it could hold is a press away, one tick each, with the count
/// and the colour only where the caller asked for them.
/// </summary>
public sealed class ValueBrowserTests : OrbitTestContext
{
    private static readonly ValueBrowser.Offer[] ThreeWords =
        [new("work", 3), new("home", 1), new("garden")];

    /// <summary>
    /// The whole point of the closed field: the words it stands for are readable without opening
    /// anything, which is what a row of buttons and a dialog both failed to do.
    /// </summary>
    [Fact]
    public void The_closed_field_writes_what_is_chosen_along_it_and_offers_nothing_until_it_is_asked()
    {
        var cut = RenderComponent<ValueBrowser>(parameters => parameters
            .Add(browser => browser.Offers, ThreeWords)
            .Add(browser => browser.Chosen, ["work", "home"]));

        Assert.Equal("work, home", cut.Find(".value-browser-written").TextContent.Trim());
        Assert.Empty(cut.FindAll(".value-browser-panel"));
        Assert.Equal("false", cut.Find(".value-browser-field").GetAttribute("aria-expanded"));
    }

    /// <summary>Nothing chosen is an invitation rather than a blank: the field says what it is for.</summary>
    [Fact]
    public void An_empty_field_says_what_it_is_for()
    {
        var cut = RenderComponent<ValueBrowser>(parameters => parameters
            .Add(browser => browser.Offers, ThreeWords)
            .Add(browser => browser.Placeholder, "All tags"));

        var written = cut.Find(".value-browser-written");
        Assert.Equal("All tags", written.TextContent.Trim());
        Assert.Contains("is-empty", written.ClassName);
    }

    /// <summary>
    /// Opened, every word is a row with its own tick - and the count beside it only where the caller
    /// counted, since on a field being filled in a number would be about other things entirely.
    /// </summary>
    [Fact]
    public void Opening_it_lists_every_word_with_its_count_where_there_is_one()
    {
        var cut = RenderComponent<ValueBrowser>(parameters => parameters
            .Add(browser => browser.Offers, ThreeWords)
            .Add(browser => browser.Chosen, ["work"]));

        cut.Find(".value-browser-field").Click();

        var rows = cut.FindAll(".value-browser-row").ToList();
        Assert.Equal(3, rows.Count);
        Assert.Equal("true", rows[0].GetAttribute("aria-selected"));
        Assert.Equal("false", rows[1].GetAttribute("aria-selected"));
        Assert.Equal(["3", "1"], cut.FindAll(".value-browser-count").Select(count => count.TextContent.Trim()));
        // Nobody asked for colours here, so no well is drawn on any row.
        Assert.Empty(cut.FindAll(".value-browser-colour"));
    }

    /// <summary>Ticking takes a word and unticking gives it back, both as the whole list the caller binds to.</summary>
    [Fact]
    public void Ticking_takes_a_word_and_unticking_gives_it_back()
    {
        IReadOnlyList<string> chosen = ["work"];
        var cut = RenderComponent<ValueBrowser>(parameters => parameters
            .Add(browser => browser.Offers, ThreeWords)
            .Add(browser => browser.Chosen, chosen)
            .Add(browser => browser.ChosenChanged, words => chosen = words));

        cut.Find(".value-browser-field").Click();
        cut.FindAll(".value-browser-row input[type=checkbox]").ToList()[1].Change(true);
        Assert.Equal(["work", "home"], chosen);

        cut.SetParametersAndRender(parameters => parameters.Add(browser => browser.Chosen, chosen));
        cut.FindAll(".value-browser-row input[type=checkbox]").ToList()[0].Change(false);
        Assert.Equal(["home"], chosen);
    }

    /// <summary>
    /// Where one answer is wanted, ticking replaces what held - and unticking the one that holds does
    /// nothing, because "read in no view at all" is not one of the answers a page can be read in.
    /// </summary>
    [Fact]
    public void Where_one_answer_is_wanted_ticking_replaces_it_and_unticking_it_does_nothing()
    {
        var raised = new List<IReadOnlyList<string>>();
        var cut = RenderComponent<ValueBrowser>(parameters => parameters
            .Add(browser => browser.Offers, ThreeWords)
            .Add(browser => browser.Chosen, ["work"])
            .Add(browser => browser.OnlyOne, true)
            .Add(browser => browser.ChosenChanged, words => raised.Add(words)));

        cut.Find(".value-browser-field").Click();
        Assert.Equal("false", cut.Find(".value-browser-panel").GetAttribute("aria-multiselectable"));

        cut.FindAll(".value-browser-row input[type=checkbox]").ToList()[1].Change(true);
        cut.FindAll(".value-browser-row input[type=checkbox]").ToList()[0].Change(false);

        Assert.Equal([["home"]], raised);
    }

    /// <summary>
    /// A word only this one thing carries is still on the list, ticked. Anything else reads as the
    /// control having lost it - and on a field being filled in that is exactly the interesting word.
    /// </summary>
    [Fact]
    public void A_chosen_word_nothing_else_carries_is_still_drawn()
    {
        var cut = RenderComponent<ValueBrowser>(parameters => parameters
            .Add(browser => browser.Offers, ThreeWords)
            .Add(browser => browser.Chosen, ["Marmalade"]));

        cut.Find(".value-browser-field").Click();

        var rows = cut.FindAll(".value-browser-row").ToList();
        Assert.Equal(4, rows.Count);
        Assert.Contains("Marmalade", rows[3].TextContent);
        Assert.Equal("true", rows[3].GetAttribute("aria-selected"));
    }

    /// <summary>A word nothing carries yet is taken the moment it is written: writing it is choosing it.</summary>
    [Fact]
    public void A_new_word_is_added_ticked_and_stays_on_the_list()
    {
        IReadOnlyList<string> chosen = [];
        var cut = RenderComponent<ValueBrowser>(parameters => parameters
            .Add(browser => browser.Offers, ThreeWords)
            .Add(browser => browser.Chosen, chosen)
            .Add(browser => browser.CanAddNew, true)
            .Add(browser => browser.ChosenChanged, words => chosen = words));

        cut.Find(".value-browser-field").Click();
        cut.Find(".value-browser-new input").Input("marmalade");
        cut.Find(".value-browser-add").Click();

        Assert.Equal(["marmalade"], chosen);
        cut.SetParametersAndRender(parameters => parameters.Add(browser => browser.Chosen, chosen));
        Assert.Equal(4, cut.FindAll(".value-browser-row").Count);
        // The box is cleared, ready for the next one - the same as the tags box it replaces.
        Assert.Equal(string.Empty, cut.Find(".value-browser-new input").GetAttribute("value"));
    }

    /// <summary>A filter never grows words: there is a box only where the caller says the field is being filled in.</summary>
    [Fact]
    public void There_is_nowhere_to_write_a_new_word_unless_the_caller_asked_for_one()
    {
        var cut = RenderComponent<ValueBrowser>(parameters => parameters.Add(browser => browser.Offers, ThreeWords));

        cut.Find(".value-browser-field").Click();

        Assert.Empty(cut.FindAll(".value-browser-new"));
    }

    /// <summary>
    /// Where the caller asks for colours, each word carries the one the account gave it - drawn on the
    /// word itself, so what is being coloured is visible while it is chosen. See TagColourBook.
    /// </summary>
    [Fact]
    public void Colours_are_drawn_on_the_word_and_a_well_is_offered_for_each()
    {
        Services.AddSingleton(new TagColourBook(new TagsApiClient(new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""[{"tag":"work","colour":"#aa3355"}]""", Encoding.UTF8, "application/json")
            }))
        {
            BaseAddress = new Uri("https://example.test/")
        })));

        var cut = RenderComponent<ValueBrowser>(parameters => parameters
            .Add(browser => browser.Offers, ThreeWords)
            .Add(browser => browser.OffersColours, true));

        cut.Find(".value-browser-field").Click();

        cut.WaitForAssertion(() => Assert.Contains("--tag-colour: #aa3355", cut.Markup));
        Assert.Equal(3, cut.FindAll(".value-browser-colour").Count);
        // Only the coloured word offers the way back to plain.
        Assert.Single(cut.FindAll(".value-browser-uncolour"));
    }

    /// <summary>Nothing has ever been filed under anything, and the panel says so rather than opening empty.</summary>
    [Fact]
    public void An_empty_vocabulary_says_so()
    {
        var cut = RenderComponent<ValueBrowser>(parameters => parameters
            .Add(browser => browser.Offers, [])
            .Add(browser => browser.EmptyMessage, "No tags yet."));

        cut.Find(".value-browser-field").Click();

        Assert.Equal("No tags yet.", cut.Find(".value-browser-empty").TextContent.Trim());
    }

    /// <summary>A reader who cannot change what they are looking at is not offered the list at all.</summary>
    [Fact]
    public void A_read_only_field_does_not_open()
    {
        var cut = RenderComponent<ValueBrowser>(parameters => parameters
            .Add(browser => browser.Offers, ThreeWords)
            .Add(browser => browser.Chosen, ["work"])
            .Add(browser => browser.IsReadOnly, true));

        var field = cut.Find(".value-browser-field");
        Assert.True(field.HasAttribute("disabled"));
        field.Click();
        Assert.Empty(cut.FindAll(".value-browser-panel"));
    }
}
