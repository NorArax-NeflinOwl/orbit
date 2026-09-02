using Orbit.Contracts.Tasks;
using Orbit.Web.Services;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// What the tasks page shows once a reader has said what they are looking for. The rules live in one
/// object because they are asked together - a search and a category both set narrow twice.
/// </summary>
public sealed class TaskItemFilterTests
{
    [Fact]
    public void Nothing_asked_for_shows_everything()
    {
        var filter = new TaskItemFilter();

        Assert.False(filter.IsActive);
        Assert.True(filter.Matches(AnEntry("Buy milk")));
        Assert.True(filter.HasAMatch(AList(AnEntry("Buy milk"))));
    }

    [Fact]
    public void A_search_matches_anywhere_in_the_words_and_ignores_case()
    {
        var filter = new TaskItemFilter { Search = "MIL" };

        Assert.True(filter.Matches(AnEntry("Buy milk")));
        Assert.False(filter.Matches(AnEntry("Buy bread")));
    }

    /// <summary>Picking a second category usually means "and also that one" - so any of them, by default.</summary>
    [Fact]
    public void Two_categories_mean_either_of_them_unless_asked_otherwise()
    {
        var filter = new TaskItemFilter();
        filter.Toggle("shopping");
        filter.Toggle("car");

        Assert.False(filter.MatchesEveryCategory);
        Assert.True(filter.Matches(AnEntry("Buy milk", "shopping")));
        Assert.True(filter.Matches(AnEntry("New tyres", "car")));
        Assert.False(filter.Matches(AnEntry("Call the dentist", "health")));
    }

    [Fact]
    public void Asking_for_every_category_means_the_entry_carries_them_all()
    {
        var filter = new TaskItemFilter { MatchesEveryCategory = true };
        filter.Toggle("shopping");
        filter.Toggle("car");

        Assert.True(filter.Matches(AnEntry("Screen wash", "shopping", "car")));
        // Carries one of the two, which is no longer enough.
        Assert.False(filter.Matches(AnEntry("Buy milk", "shopping")));
    }

    /// <summary>A category is one word however it was capitalised - see TaskItem.Categories.</summary>
    [Fact]
    public void A_category_matches_however_it_was_written()
    {
        var filter = new TaskItemFilter();
        filter.Toggle("Shopping");

        Assert.True(filter.Matches(AnEntry("Buy milk", "shopping")));
        Assert.True(filter.IsChosen("SHOPPING"));
    }

    [Fact]
    public void Pressing_a_chosen_category_again_unchooses_it()
    {
        var filter = new TaskItemFilter();
        filter.Toggle("shopping");
        filter.Toggle("shopping");

        Assert.Empty(filter.Categories);
        Assert.False(filter.IsActive);
    }

    [Fact]
    public void A_search_and_a_category_both_have_to_hold()
    {
        var filter = new TaskItemFilter { Search = "milk" };
        filter.Toggle("shopping");

        Assert.True(filter.Matches(AnEntry("Buy milk", "shopping")));
        // The right words, filed elsewhere.
        Assert.False(filter.Matches(AnEntry("Buy milk", "work")));
        // The right category, different words.
        Assert.False(filter.Matches(AnEntry("Buy bread", "shopping")));
    }

    [Fact]
    public void A_list_is_worth_showing_when_one_entry_on_it_matches()
    {
        var filter = new TaskItemFilter { Search = "milk" };

        Assert.True(filter.HasAMatch(AList(AnEntry("Buy bread"), AnEntry("Buy milk"))));
        Assert.False(filter.HasAMatch(AList(AnEntry("Buy bread"))));
    }

    [Fact]
    public void Clearing_puts_everything_back()
    {
        var filter = new TaskItemFilter { Search = "milk", MatchesEveryCategory = true };
        filter.Toggle("shopping");

        filter.Clear();

        Assert.False(filter.IsActive);
        Assert.False(filter.MatchesEveryCategory);
        Assert.Empty(filter.Categories);
    }

    private static TaskDto AList(params TaskItemDto[] items)
        => new(
            Guid.NewGuid(), "Errands", items, IsCompleted: false, IsGroup: false, IsPrivate: false,
            EncryptedContent: null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit", OriginalOwnerUserId: null);

    private static TaskItemDto AnEntry(string description, params string[] categories)
        => new(
            Guid.NewGuid(), description, DueDateUtc: null, IsCompleted: false, LinkedTaskListId: null,
            OverdueNotificationChannel: "None", RemindDaily: false,
            DailyReminderNotificationChannel: "None", DailyReminderTimeOfDay: default,
            Categories: categories);
}
