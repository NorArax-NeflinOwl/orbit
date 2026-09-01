using Orbit.Core;
using Orbit.Core.Abstractions;
using Orbit.Core.Tasks;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

/// <summary>
/// What an entry is filed under. Free text like a shelf item's category, but several of them - and
/// tidied on the way in, because a filter can only gather what was written the same way twice.
/// </summary>
public sealed class TaskItemCategoryTests
{
    [Fact]
    public void An_entry_is_filed_under_nothing_until_somebody_says_otherwise()
        => Assert.Empty(TaskItem.Create("Buy milk", dueDateUtc: null, isCompleted: false).Categories);

    [Fact]
    public void The_categories_are_kept_in_the_order_they_were_given()
    {
        var item = AnEntryFiledUnder("shopping", "car");

        Assert.Equal(["shopping", "car"], item.Categories);
    }

    [Fact]
    public void Blanks_and_edges_are_not_worth_storing()
    {
        var item = AnEntryFiledUnder("  shopping  ", "   ", "");

        Assert.Equal(["shopping"], item.Categories);
    }

    /// <summary>
    /// One word said twice is one category. A filter compares them without regard to case (see
    /// TaskItemFilter), so keeping both would show the reader two chips meaning the same thing.
    /// </summary>
    [Fact]
    public void The_same_category_written_two_ways_is_kept_once()
    {
        var item = AnEntryFiledUnder("Shopping", "shopping");

        Assert.Equal(["Shopping"], item.Categories);
    }

    [Fact]
    public void A_category_longer_than_the_column_is_refused_rather_than_cut()
    {
        var tooLong = new string('a', StoredTextLimits.Category + 1);

        Assert.Throws<InvalidRequestException>(() => AnEntryFiledUnder(tooLong));
    }

    /// <summary>An entry renamed to settle an id clash keeps what it is about - see TaskItem.WithNewId.</summary>
    [Fact]
    public void A_renamed_entry_is_still_about_the_same_things()
    {
        var item = AnEntryFiledUnder("shopping");

        Assert.Equal(item.Categories, item.WithNewId().Categories);
    }

    /// <summary>Every kind of entry carries them: an appointment is about something the same way an errand is.</summary>
    [Fact]
    public void An_appointment_is_filed_the_same_way_an_errand_is()
    {
        var appointment = TaskItem.Create(
            "Dentist", dueDateUtc: null, isCompleted: false, kind: TaskItemKind.Calendar, categories: ["health"]);

        Assert.Equal(["health"], appointment.Categories);
    }

    private static TaskItem AnEntryFiledUnder(params string[] categories)
        => TaskItem.Create("Buy milk", dueDateUtc: null, isCompleted: false, categories: categories);
}
