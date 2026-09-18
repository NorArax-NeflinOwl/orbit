using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Orbit.Contracts.Tags;
using Orbit.Web.Components;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Components;

/// <summary>
/// Tags on a card and in the tags field, each in the colour the account gave it - see TagColourBook. The
/// stub answers the way the server does: one colour per tag whatever its case, every colour in the answer
/// to a set, and an empty colour taking one away.
/// </summary>
public sealed class TagChipsTests : OrbitTestContext
{
    private readonly List<SetTagColourRequest> _set = [];
    private List<TagColourDto> _colours = [new("work", "#aa3355")];

    public TagChipsTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(new TagsApiClient(new HttpClient(new StubHttpMessageHandler(Answer))
        {
            BaseAddress = new Uri("https://example.test/")
        }));
    }

    [Fact]
    public void A_tag_is_drawn_in_the_colour_the_account_gave_it_whatever_its_case()
    {
        var cut = RenderComponent<TagChips>(parameters => parameters.Add(chips => chips.Tags, ["Work", "home"]));

        cut.WaitForAssertion(() => Assert.Contains("--tag-colour: #aa3355", cut.Markup));
        var chips = cut.FindAll(".card-badge-tag").ToList();
        Assert.Equal(2, chips.Count);
        Assert.Contains("card-badge-tag-coloured", chips[0].ClassName);
        // A tag nobody coloured is drawn plain rather than in some default the reader never chose.
        Assert.DoesNotContain("card-badge-tag-coloured", chips[1].ClassName);
    }

    /// <summary>
    /// The colour goes into a style attribute, so only a colour is let in: anything else the server might
    /// answer - a stored value from before the check, or a server that is not Orbit's - is drawn plain.
    /// </summary>
    [Fact]
    public void A_colour_that_is_not_hex_is_never_written_into_a_style()
    {
        _colours = [new("work", "red; background: url(https://example.test/x)")];

        var cut = RenderComponent<TagChips>(parameters => parameters.Add(chips => chips.Tags, ["work"]));

        cut.WaitForAssertion(() => Assert.Null(cut.Find(".card-badge-tag").GetAttribute("style")));
    }

    /// <summary>
    /// A colour is picked in the tags field - in the browser the tags are chosen in, beside the word
    /// itself - and saved there and then for the whole account, since it belongs to the word rather
    /// than to the note being edited. The word takes it at once.
    /// </summary>
    [Fact]
    public void Picking_a_colour_saves_it_for_the_account_and_the_word_takes_it()
    {
        var cut = RenderComponent<TagsField>(parameters => parameters.Add(field => field.Values, ["home"]));

        cut.Find(".value-browser-field").Click();
        // This word's own well: the panel also lists "work", which the account has already coloured.
        RowFor(cut, "home").QuerySelector(".value-browser-colour")!.Change("#113355");

        var asked = Assert.Single(_set);
        Assert.Equal(("home", "#113355"), (asked.Tag, asked.Colour));
        cut.WaitForAssertion(() => Assert.Contains(
            "--tag-colour: #113355", RowFor(cut, "home").QuerySelector(".value-browser-name")!.GetAttribute("style")));
    }

    /// <summary>
    /// Said where the colour is chosen: the colour store is readable on the server, and names a tag even
    /// when only private items carry it - see info/functionality.md, "Tags and their colours".
    /// </summary>
    [Fact]
    public void The_colour_well_says_the_colour_is_readable_on_the_server()
    {
        var cut = RenderComponent<TagsField>(parameters => parameters.Add(field => field.Values, ["home"]));

        cut.Find(".value-browser-field").Click();

        Assert.Contains("readable on the server", cut.Markup);
    }

    /// <summary>
    /// The words are readable without opening anything, which is what the row of chips and the row of
    /// colour wells under it could not do once there were more than a few - see ValueBrowser.
    /// </summary>
    [Fact]
    public void The_tags_a_thing_carries_are_written_along_the_closed_field()
    {
        var cut = RenderComponent<TagsField>(parameters => parameters.Add(field => field.Values, ["home", "work"]));

        Assert.Equal("home, work", cut.Find(".value-browser-written").TextContent.Trim());
    }

    /// <summary>One word's row in the open browser, by the word itself - see ValueBrowser.</summary>
    private static AngleSharp.Dom.IElement RowFor(IRenderedFragment cut, string word)
        => cut.FindAll(".value-browser-row")
            .First(row => row.QuerySelector(".value-browser-name")!.TextContent.Trim() == word);

    private HttpResponseMessage Answer(HttpRequestMessage request)
    {
        if (request.Method == HttpMethod.Put)
        {
            var asked = JsonSerializer.Deserialize<SetTagColourRequest>(
                request.Content!.ReadAsStringAsync().GetAwaiter().GetResult(),
                new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            _set.Add(asked);
            _colours =
            [
                .. _colours.Where(colour => !string.Equals(colour.Tag, asked.Tag, StringComparison.OrdinalIgnoreCase)),
                .. asked.Colour.Length > 0 ? [new TagColourDto(asked.Tag, asked.Colour)] : Array.Empty<TagColourDto>()
            ];
        }

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(_colours) };
    }
}
