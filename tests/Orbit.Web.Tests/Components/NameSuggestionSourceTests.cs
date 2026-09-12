using System.Net;
using System.Text;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Orbit.Core.Suggestions;
using Orbit.Web.Components;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Components;

/// <summary>
/// A suggested name that is the name of something - an entry on another list, a product on a shelf - is
/// offered once per thing, saying where it is, and picking it hands over the thing rather than the words.
/// See NameSuggestions.OnSourceChosen and TaskItem.ReferencesTaskItemId.
/// </summary>
public sealed class NameSuggestionSourceTests : OrbitTestContext
{
    private static readonly Guid SourceId = Guid.NewGuid();

    public NameSuggestionSourceTests()
    {
        // The panel is placed and wired to the keys through JS, which a test renderer has none of.
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(new NameSuggestionsApiClient(new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "[{\"name\":\"Sauce\",\"similarity\":0.6,\"sources\":[{\"kind\":\"TaskItem\",\"id\":\"" + SourceId
                    + "\",\"itemId\":\"" + SourceId + "\",\"containerId\":\"" + Guid.NewGuid()
                    + "\",\"containerName\":\"Burger\",\"entryKind\":\"Checklist\"}]}]",
                    Encoding.UTF8, "application/json")
            }))
        {
            BaseAddress = new Uri("https://example.test/")
        }));
    }

    [Fact]
    public void A_name_of_something_says_where_it_is_and_hands_that_thing_over()
    {
        NameSuggestionPick? picked = null;
        var cut = RenderComponent<NameSuggestions>(parameters => parameters
            .Add(field => field.Kind, NameSuggestionKind.TaskItemDescription)
            // What the field opens on is taken as already there, not as typing - see OnParametersSet.
            .Add(field => field.Value, "S")
            .Add(field => field.OnSourceChosen, EventCallback.Factory.Create<NameSuggestionPick>(this, pick => picked = pick)));

        cut.SetParametersAndRender(parameters => parameters.Add(field => field.Value, "Sau"));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".name-suggestion-option")));

        var option = cut.Find(".name-suggestion-option");
        Assert.Contains("in Burger", option.TextContent, StringComparison.Ordinal);
        option.Click();

        Assert.Equal(SourceId, picked!.Source.Id);
        Assert.Equal("Sauce", picked.Name);
    }

    /// <summary>A kind switched off on the Preferences tab is offered as its words alone - see DevicePreferences.KindsFilledFromSuggestions.</summary>
    [Fact]
    public void A_kind_switched_off_is_offered_as_words_only()
    {
        string? chosen = null;
        NameSuggestionPick? picked = null;
        var cut = RenderComponent<NameSuggestions>(parameters => parameters
            .Add(field => field.Kind, NameSuggestionKind.TaskItemDescription)
            // What the field opens on is taken as already there, not as typing - see OnParametersSet.
            .Add(field => field.Value, "S")
            .Add(field => field.SourceKinds, new HashSet<string> { "Inventory" })
            .Add(field => field.OnChosen, EventCallback.Factory.Create<string>(this, name => chosen = name))
            .Add(field => field.OnSourceChosen, EventCallback.Factory.Create<NameSuggestionPick>(this, pick => picked = pick)));

        cut.SetParametersAndRender(parameters => parameters.Add(field => field.Value, "Sau"));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".name-suggestion-option")));

        Assert.DoesNotContain("in Burger", cut.Find(".name-suggestion-option").TextContent, StringComparison.Ordinal);
        cut.Find(".name-suggestion-option").Click();

        Assert.Equal("Sauce", chosen);
        Assert.Null(picked);
    }
}
