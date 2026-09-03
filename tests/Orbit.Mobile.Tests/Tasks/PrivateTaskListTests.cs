using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Tasks;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Tasks;

/// <summary>
/// What a private list shows on the tasks screen while private things are locked. The notes screen has
/// hidden them behind the device lock since the gate existed and this one did not, so a list said its
/// name where the note beside it said "Private" - the counterpart of PrivateNoteTests, and the same
/// rule: the row stays, because one that vanished would read as deleted.
/// </summary>
public sealed class PrivateTaskListTests
{
    [Fact]
    public void A_private_list_says_nothing_about_itself_while_locked()
    {
        var row = Describe(
            new LocalTaskList { Title = "Bank paperwork", IsPrivate = true }, privateItemsAreUnlocked: false);

        Assert.True(row.IsHidden);
        Assert.Equal("Private", row.DisplayTitle);
        Assert.False(row.CanBeOpened);
    }

    /// <summary>Everything the heading would otherwise carry goes with the title - a priority badge saying "High" on a hidden row is still something about it.</summary>
    [Fact]
    public void A_hidden_row_carries_neither_badges_nor_the_card_below_it()
    {
        var row = Describe(
            new LocalTaskList { Title = "Bank paperwork", IsPrivate = true, Priority = "High" },
            privateItemsAreUnlocked: false) with { CanBeMoved = true };

        Assert.False(row.HasBadges);
        Assert.False(row.HasPriorityBadge);
        Assert.False(row.CanBeArranged);
        Assert.False(row.IsExpanded);
    }

    [Fact]
    public void The_same_list_reads_normally_once_unlocked()
    {
        var row = Describe(
            new LocalTaskList { Title = "Bank paperwork", IsPrivate = true }, privateItemsAreUnlocked: true);

        Assert.False(row.IsHidden);
        Assert.Equal("Bank paperwork", row.DisplayTitle);
        Assert.True(row.CanBeOpened);
    }

    [Fact]
    public void An_ordinary_list_is_never_hidden()
    {
        var row = Describe(new LocalTaskList { Title = "Shopping" }, privateItemsAreUnlocked: false);

        Assert.False(row.IsHidden);
        Assert.Equal("Shopping", row.DisplayTitle);
    }

    private static TaskListRow Describe(LocalTaskList taskList, bool privateItemsAreUnlocked)
        => TaskListRow.From(
            taskList, [taskList], hasUnsentChanges: false, FixedNetworkStatus.Online,
            new Translations(new InMemoryLanguageStore()), privateItemsAreUnlocked);
}
