using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Users.UpdateProfile;
using Orbit.Core.Users;
using Orbit.Core;
using Orbit.Core.Abstractions;
using Orbit.Core.Calendar;
using Orbit.Core.Chat.Groups;
using Orbit.Core.Inventory;
using Orbit.Core.Notes;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks;
using Xunit;

namespace Orbit.Api.Tests;

/// <summary>
/// What happens to text that is longer than Orbit can store.
///
/// Every one of these limits existed only as a column width, so the database was the thing that refused
/// - and it refuses by raising "value too long for type character varying(200)", which reached the
/// caller as a 500. A 500 says Orbit is broken; what actually happened is that somebody pasted a
/// paragraph into a box meant for a name, which is a thing they can fix as soon as anybody tells them.
///
/// The rule lives in the domain now and the schema reads the same numbers (see OrbitDbContext), so the
/// two cannot drift into disagreeing about what fits.
/// </summary>
public sealed class StoredTextLimitsTests
{
    private static string TooLongFor(int limit) => new('a', limit + 1);

    private static string LongestAllowed(int limit) => new('a', limit);

    [Fact]
    public void A_note_title_that_would_not_fit_is_refused_rather_than_stored()
    {
        var refusal = Assert.Throws<InvalidRequestException>(
            () => Note.Create(Guid.NewGuid(), TooLongFor(StoredTextLimits.Title), []));

        // The message has to name the box, because a save can carry several at once.
        Assert.Contains("note's title", refusal.Message);
        Assert.Contains(StoredTextLimits.Title.ToString(), refusal.Message);
    }

    /// <summary>Exactly at the limit is allowed - an off-by-one here would refuse text that fits.</summary>
    [Fact]
    public void A_note_title_of_exactly_the_limit_is_fine()
        => Assert.Equal(
            LongestAllowed(StoredTextLimits.Title),
            Note.Create(Guid.NewGuid(), LongestAllowed(StoredTextLimits.Title), []).Title);

    [Fact]
    public void A_task_list_title_that_would_not_fit_is_refused()
        => Assert.Throws<InvalidRequestException>(
            () => TaskList.Create(Guid.NewGuid(), TooLongFor(StoredTextLimits.Title), []));

    [Fact]
    public void A_line_of_work_that_would_not_fit_is_refused()
        => Assert.Throws<InvalidRequestException>(
            () => TaskItem.Create(TooLongFor(StoredTextLimits.TaskDescription), dueDateUtc: null, isCompleted: false));

    [Fact]
    public void An_entrys_place_that_would_not_fit_is_refused()
        => Assert.Throws<InvalidRequestException>(
            () => TaskItem.Create(
                "Dentist", dueDateUtc: null, isCompleted: false,
                kind: TaskItemKind.Calendar, location: TooLongFor(StoredTextLimits.Address)));

    [Fact]
    public void A_warehouse_name_that_would_not_fit_is_refused()
        => Assert.Throws<InvalidRequestException>(
            () => Warehouse.Create(Guid.NewGuid(), TooLongFor(StoredTextLimits.Title)));

    [Fact]
    public void A_group_name_that_would_not_fit_is_refused()
        => Assert.Throws<InvalidRequestException>(
            () => ChatGroup.Create(Guid.NewGuid(), TooLongFor(StoredTextLimits.GroupName)));

    [Theory]
    [InlineData("name")]
    [InlineData("type")]
    [InlineData("category")]
    public void A_shelf_items_words_that_would_not_fit_are_refused(string tooLongOne)
        => Assert.Throws<InvalidRequestException>(() => InventoryItem.Create(
            Guid.NewGuid(),
            tooLongOne == "name" ? TooLongFor(StoredTextLimits.Title) : "Pasta",
            tooLongOne == "type" ? TooLongFor(StoredTextLimits.ProductType) : "Dry",
            tooLongOne == "category" ? TooLongFor(StoredTextLimits.Category) : "Food",
            quantity: 1, minimumQuantity: null, InventoryUnit.Piece,
            expiryDate: null, NotificationChannel.None));

    /// <summary>
    /// The event carries four of these, and all four are checked wherever its time range already is -
    /// so changing an event is refused the same way creating one is.
    /// </summary>
    [Theory]
    [InlineData("title")]
    [InlineData("description")]
    [InlineData("colour")]
    [InlineData("address")]
    public void An_events_words_that_would_not_fit_are_refused(string tooLongOne)
        => Assert.Throws<InvalidRequestException>(
            () => CalendarEvent.Create(Guid.NewGuid(), DetailsWithOneTooLong(tooLongOne)));

    [Theory]
    [InlineData("title")]
    [InlineData("description")]
    [InlineData("colour")]
    [InlineData("address")]
    public void Changing_an_event_refuses_them_too(string tooLongOne)
    {
        var calendarEvent = CalendarEvent.Create(Guid.NewGuid(), DetailsWithOneTooLong(fitting: true));

        Assert.Throws<InvalidRequestException>(() => calendarEvent.Update(DetailsWithOneTooLong(tooLongOne)));
    }

    private static CalendarEventDetails DetailsWithOneTooLong(string tooLongOne = "", bool fitting = false)
        => new(
            !fitting && tooLongOne == "title" ? TooLongFor(StoredTextLimits.Title) : "Dentist",
            !fitting && tooLongOne == "description" ? TooLongFor(StoredTextLimits.EventDescription) : "About a tooth",
            new EventLocation(
                !fitting && tooLongOne == "address" ? TooLongFor(StoredTextLimits.Address) : "Długa 4",
                52.23, 21.01),
            !fitting && tooLongOne == "colour" ? TooLongFor(StoredTextLimits.Color) : "#ffffff",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1),
            IsAllDay: false, Recurrence: null, Guests: [], ReminderMinutesBeforeStart: [],
            NotificationChannel.None, NotificationChannel.None);

    /// <summary>
    /// The two an account is known by. Missed on the first pass at this - the probe that found the rest
    /// asked the wrong endpoint and got a 405, which reads like "refused" and is not.
    /// </summary>
    [Theory]
    [InlineData("display name")]
    [InlineData("login")]
    public async Task What_an_account_is_called_is_refused_when_it_would_not_fit(string tooLongOne)
    {
        var users = new InMemoryUserRepository();
        var user = User.Create("someone@example.test", "someone", "Someone", "hash");
        await users.AddAsync(user, CancellationToken.None);
        var handler = new UpdateProfileCommandHandler(users);

        await Assert.ThrowsAsync<InvalidRequestException>(() => handler.HandleAsync(
            new UpdateProfileCommand(
                user.Id,
                tooLongOne == "display name" ? TooLongFor(StoredTextLimits.DisplayName) : "Someone",
                tooLongOne == "login" ? TooLongFor(StoredTextLimits.UserName) : "someone"),
            CancellationToken.None));
    }
}
