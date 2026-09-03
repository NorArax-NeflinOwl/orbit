using Bunit;
using Orbit.Web.Components;
using Xunit;

namespace Orbit.Web.Tests.Components;

/// <summary>
/// All X · Edit-or-View · Share · Delete, in that order. What matters here is that a slot a page leaves
/// out is left out of the menu entirely - not drawn and disabled - since a button that always refuses is
/// not a choice; the six pages that carry this decide the wording and whether a slot applies at all,
/// this only draws whatever they hand it.
/// </summary>
public sealed class ObjectMenuTests : OrbitTestContext
{
    [Fact]
    public void Only_edit_is_offered_when_nothing_else_is_handed_to_it()
    {
        var cut = RenderComponent<ObjectMenu>(parameters => parameters
            .Add(menu => menu.EditLabel, "Edit"));

        var entries = Open(cut);

        Assert.Equal(["Edit"], entries);
    }

    [Fact]
    public void All_share_and_delete_read_in_order_when_every_slot_is_filled()
    {
        var cut = RenderComponent<ObjectMenu>(parameters => parameters
            .Add(menu => menu.AllLabel, "All notes")
            .Add(menu => menu.EditLabel, "Edit")
            .Add(menu => menu.ShareLabel, "Share")
            .Add(menu => menu.DeleteLabel, "Delete"));

        var entries = Open(cut);

        Assert.Equal(["All notes", "Edit", "Share", "Delete"], entries);
    }

    [Fact]
    public void Each_slot_calls_its_own_handler()
    {
        var pressed = "";
        var cut = RenderComponent<ObjectMenu>(parameters => parameters
            .Add(menu => menu.AllLabel, "All notes").Add(menu => menu.OnAll, () => pressed = "all")
            .Add(menu => menu.EditLabel, "Edit").Add(menu => menu.OnEdit, () => pressed = "edit")
            .Add(menu => menu.DeleteLabel, "Delete").Add(menu => menu.OnDelete, () => pressed = "delete"));

        cut.Find(".overflow-menu-trigger").Click();
        cut.FindAll(".avatar-dropdown-item").First(entry => entry.TextContent.Trim() == "Delete").Click();

        Assert.Equal("delete", pressed);
    }

    /// <summary>A note somebody else owns is not this reader's to delete: the label changes rather than
    /// the button disappearing, since taking it off this reader's own list is still something to do
    /// here - see Notes.razor.</summary>
    [Fact]
    public void Delete_can_read_as_something_else_without_disappearing()
    {
        var cut = RenderComponent<ObjectMenu>(parameters => parameters
            .Add(menu => menu.EditLabel, "View")
            .Add(menu => menu.DeleteLabel, "Remove from my list"));

        Assert.Contains("Remove from my list", Open(cut));
    }

    [Fact]
    public void Delete_can_be_greyed_without_disappearing()
    {
        var cut = RenderComponent<ObjectMenu>(parameters => parameters
            .Add(menu => menu.EditLabel, "Edit")
            .Add(menu => menu.DeleteLabel, "Delete")
            .Add(menu => menu.DeleteDisabled, true));

        cut.Find(".overflow-menu-trigger").Click();

        Assert.True(cut.FindAll(".avatar-dropdown-item").First(entry => entry.TextContent.Trim() == "Delete").HasAttribute("disabled"));
    }

    /// <summary>Entries only exist once the trigger has been pressed - see OverflowMenu, which draws
    /// nothing else until then.</summary>
    private static List<string> Open(IRenderedComponent<ObjectMenu> cut)
    {
        cut.Find(".overflow-menu-trigger").Click();
        return cut.FindAll(".avatar-dropdown-item").Select(entry => entry.TextContent.Trim()).ToList();
    }
}
