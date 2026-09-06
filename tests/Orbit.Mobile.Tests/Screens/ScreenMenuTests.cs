using Orbit.Mobile.Screens;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// The three-dot menu every screen keeps its extra actions behind - Orbit.Web's OverflowMenu, drawn on
/// the phone by one panel per screen rather than one per card.
///
/// What these pin down is the part that is easy to get wrong once the panel is shared: a menu opened
/// from somewhere else must not show the last one's entries, and an action must close the menu behind
/// it while a setting must not.
/// </summary>
public sealed class ScreenMenuTests
{
    [Fact]
    public void A_menu_with_nothing_in_it_does_not_open()
    {
        var menu = new ScreenMenu();

        menu.Show([]);

        Assert.False(menu.IsOpen);
    }

    [Fact]
    public void Opening_it_somewhere_else_replaces_everything_the_last_one_left()
    {
        var menu = new ScreenMenu();
        menu.Show([new ScreenMenuEntry("Delete line", () => { })], "Line options");

        menu.Show([new ScreenMenuEntry("By when", () => { })], "Sort");

        Assert.Equal("Sort", menu.Heading);
        Assert.Equal("By when", Assert.Single(menu.Entries).Label);
    }

    [Fact]
    public void An_action_closes_the_menu_behind_it()
    {
        var chosen = false;
        var menu = new ScreenMenu();
        menu.Show([new ScreenMenuEntry("Delete note", () => chosen = true)]);

        menu.Entries[0].ChooseCommand.Execute(null);

        Assert.True(chosen);
        Assert.False(menu.IsOpen);
    }

    [Fact]
    public void A_setting_leaves_it_open_so_that_changing_two_is_not_a_chore()
    {
        var menu = new ScreenMenu();
        menu.Show([new ScreenMenuEntry("By type", () => { }, staysOpen: true)]);

        menu.Entries[0].ChooseCommand.Execute(null);

        Assert.True(menu.IsOpen);
    }

    [Fact]
    public void An_entry_that_cannot_do_anything_yet_does_nothing_when_pressed()
    {
        var chosen = false;
        var menu = new ScreenMenu();
        menu.Show([new ScreenMenuEntry("Share", () => chosen = true, canBeChosen: false)]);

        menu.Entries[0].ChooseCommand.Execute(null);

        Assert.False(chosen);
        Assert.True(menu.IsOpen);
    }

    [Fact]
    public void The_chosen_entry_carries_the_tick_and_the_others_carry_its_column()
    {
        var menu = new ScreenMenu();
        menu.Show(
        [
            new ScreenMenuEntry("By when", () => { }, isChosen: true),
            new ScreenMenuEntry("Alphabetical", () => { })
        ]);

        Assert.Equal("✓", menu.Entries[0].Mark);
        Assert.Equal(string.Empty, menu.Entries[1].Mark);
    }

    [Fact]
    public void Pressing_outside_it_shuts_it()
    {
        var menu = new ScreenMenu();
        menu.Show([new ScreenMenuEntry("Delete note", () => { })]);

        menu.CloseCommand.Execute(null);

        Assert.False(menu.IsOpen);
    }
}
