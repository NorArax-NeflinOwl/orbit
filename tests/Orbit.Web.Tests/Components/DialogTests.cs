using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Orbit.Web.Components;
using Xunit;

namespace Orbit.Web.Tests.Components;

/// <summary>
/// Three ways out of a dialog, because a panel that has to be dismissed one particular way is a panel
/// somebody gets stuck in - and one way in that must not be a way out: a press on the panel itself.
/// </summary>
public sealed class DialogTests : OrbitTestContext
{
    [Fact]
    public void The_backdrop_the_cross_and_Escape_all_close_it()
    {
        var closedCount = 0;
        var cut = Render(closed: () => closedCount++);

        cut.Find(".dialog-overlay").Click();
        cut.Find(".dialog-header button").Click();
        cut.Find(".dialog-panel").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Equal(3, closedCount);
    }

    [Fact]
    public void Pressing_inside_the_panel_does_not_close_it()
    {
        var cut = Render(closed: () => { });

        // The press stops at the panel rather than reaching the backdrop behind it - otherwise reading
        // the dialog by clicking into it would dismiss it. Asserted on the markup rather than by
        // clicking: with no handler of its own the panel takes no click here, which is the whole point
        // of it - Blazor's own delegation is what stops the press in a browser.
        Assert.True(cut.Find(".dialog-panel").HasAttribute("blazor:onclick:stoppropagation"));
    }

    [Fact]
    public void Any_other_key_leaves_it_open()
    {
        var closedCount = 0;
        var cut = Render(closed: () => closedCount++);

        cut.Find(".dialog-panel").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(0, closedCount);
    }

    [Fact]
    public void The_footer_is_left_out_entirely_for_a_dialog_that_only_gets_read()
    {
        var cut = Render(closed: () => { });

        Assert.Empty(cut.FindAll(".dialog-footer"));
        Assert.Contains("Everything about it", cut.Find(".dialog-body").TextContent);
    }

    private IRenderedComponent<Dialog> Render(Action closed)
        => RenderComponent<Dialog>(parameters => parameters
            .Add(dialog => dialog.Title, "About Orbit")
            .Add(dialog => dialog.ChildContent, "<p>Everything about it</p>")
            .Add(dialog => dialog.OnClosed, closed));
}
