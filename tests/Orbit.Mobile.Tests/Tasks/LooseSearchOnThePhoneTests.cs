using Orbit.Contracts.Tasks;
using Orbit.Mobile.Screens.Tasks;
using Xunit;

namespace Orbit.Mobile.Tests.Tasks;

/// <summary>
/// The phone's searches ignore the marks over Polish letters, as the browser's do (Orbit.Core.Text.LooseText):
/// a search box that insists on "ą" finds nothing until somebody holds the right key. Asked for on the list of
/// 2026-09-16, where the browser already did this and the phone compared letter for letter. The shelf
/// searches (InventoryItemFilter, InventoryViewModel, InventoryItemEditor) ask the same rule.
/// </summary>
public sealed class LooseSearchOnThePhoneTests
{
    [Theory]
    [InlineData("Żółw", "zolw")]
    [InlineData("Żółw", "ŻÓŁW")]
    [InlineData("maka pszenna", "mąka")]
    [InlineData("Mąka pszenna", "maka")]
    public void An_entry_is_found_whichever_side_carries_the_marks(string written, string typed)
    {
        var filter = new TaskItemFilter { Search = typed };

        Assert.True(filter.Matches(Entry(written)));
    }

    [Fact]
    public void A_different_word_is_still_not_found()
    {
        Assert.False(new TaskItemFilter { Search = "mleko" }.Matches(Entry("Mąka pszenna")));
    }

    private static TaskItemDto Entry(string description)
        => new(Guid.NewGuid(), description, null, false, null, "None", false, "None", new TimeOnly(9, 0));
}
