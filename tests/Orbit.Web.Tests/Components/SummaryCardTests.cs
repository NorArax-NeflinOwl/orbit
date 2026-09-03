using Bunit;
using Microsoft.AspNetCore.Components;
using Orbit.Web.Components;
using Xunit;

namespace Orbit.Web.Tests.Components;

/// <summary>
/// The rung-2 card: press what it says and the form opens. What matters here is that a press anywhere
/// on it opens the form, that the keyboard does the same as a click - Enter and Space, the way a real
/// button answers both - and that a key nobody meant as "open this" does nothing.
/// </summary>
public sealed class SummaryCardTests : OrbitTestContext
{
    [Fact]
    public void A_click_opens_it()
    {
        var opened = false;
        var cut = RenderComponent<SummaryCard>(parameters => parameters
            .Add(card => card.OnOpen, () => opened = true)
            .AddChildContent("<p>What the note says</p>"));

        cut.Find(".card").Click();

        Assert.True(opened);
        Assert.Contains("What the note says", cut.Find(".card").TextContent);
    }

    [Theory]
    [InlineData("Enter")]
    [InlineData(" ")]
    public void Enter_and_space_open_it_the_same_as_a_click(string key)
    {
        var opened = false;
        var cut = RenderComponent<SummaryCard>(parameters => parameters
            .Add(card => card.OnOpen, () => opened = true));

        cut.Find(".card").KeyDown(key);

        Assert.True(opened);
    }

    [Fact]
    public void A_key_that_is_not_enter_or_space_does_nothing()
    {
        var opened = false;
        var cut = RenderComponent<SummaryCard>(parameters => parameters
            .Add(card => card.OnOpen, () => opened = true));

        cut.Find(".card").KeyDown("Tab");

        Assert.False(opened);
    }

    /// <summary>NoteSummary's own spacing and cursor-default selector for a tick row, which nothing
    /// else here carries - see .note-summary-opens in app.css.</summary>
    [Fact]
    public void A_page_can_add_its_own_class_beside_the_shared_one()
    {
        var cut = RenderComponent<SummaryCard>(parameters => parameters
            .Add(card => card.AdditionalClass, "checklist-card note-summary-opens"));

        var classes = cut.Find(".card").ClassName;
        Assert.Contains("summary-opens", classes);
        Assert.Contains("checklist-card", classes);
        Assert.Contains("note-summary-opens", classes);
    }
}
