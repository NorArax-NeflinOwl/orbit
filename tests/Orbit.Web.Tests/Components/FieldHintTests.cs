using Bunit;
using Orbit.Web.Components;
using Xunit;

namespace Orbit.Web.Tests.Components;

/// <summary>
/// What a field is for, behind a "?" beside its label. Whether the bubble is *seen* is CSS's answer -
/// hover has no meaning here - so what these pin is the part that has to hold at every width: the words
/// are in the page for a screen reader whatever the mark has been doing, and pressing the mark says so,
/// which is the only way in on a phone.
/// </summary>
public sealed class FieldHintTests : OrbitTestContext
{
    [Fact]
    public void The_words_are_in_the_page_before_anybody_asks()
    {
        var cut = RenderComponent<FieldHint>(parameters => parameters
            .AddChildContent("Sorts this note against the others."));

        Assert.Equal("Sorts this note against the others.", cut.Find(".field-hint-bubble").TextContent);
    }

    /// <summary>
    /// A phone has no hover, so the mark is a button and pressing it is the way in. The state is said on
    /// the mark rather than by drawing the bubble only then - a bubble that exists only after a press is
    /// a bubble a screen reader never announces.
    /// </summary>
    [Fact]
    public void Pressing_the_mark_opens_it_and_pressing_it_again_shuts_it()
    {
        var cut = RenderComponent<FieldHint>(parameters => parameters.AddChildContent("Why this is here."));

        cut.Find(".field-hint-mark").Click();

        Assert.Equal("true", cut.Find(".field-hint-mark").GetAttribute("aria-expanded"));
        Assert.Contains("on", cut.Find(".field-hint-mark").ClassList);

        cut.Find(".field-hint-mark").Click();

        Assert.Equal("false", cut.Find(".field-hint-mark").GetAttribute("aria-expanded"));
        Assert.DoesNotContain("on", cut.Find(".field-hint-mark").ClassList);
    }

    /// <summary>
    /// A button rather than something that submits: these sit inside forms, and a bare button in a form
    /// submits it - asking what a field is for would have saved the half-filled thing being asked about.
    /// </summary>
    [Fact]
    public void The_mark_never_submits_the_form_it_stands_in()
    {
        var cut = RenderComponent<FieldHint>(parameters => parameters.AddChildContent("Why this is here."));

        Assert.Equal("button", cut.Find(".field-hint-mark").GetAttribute("type"));
    }
}
