using Orbit.Contracts.Calendar;
using Orbit.Contracts.Tasks;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Tests.TestDoubles;
using Orbit.Mobile.Widgets;
using Xunit;

namespace Orbit.Mobile.Tests.Widgets;

/// <summary>
/// The third home screen widget: whichever of the dashboard's cards the reader picked when they placed
/// it. Asked for on 2026-09-20 as "any tile from the dashboard".
///
/// Pinned for the reason the other two are - nothing about a widget is observable from inside the app -
/// and with one rule tested harder than any other: a private or sealed thing is never named on a home
/// screen, whichever card is showing.
/// </summary>
public sealed class CardAtAGlanceTests
{
    private static readonly DateTimeOffset Morning = Local(new DateTime(2026, 9, 1, 8, 0, 0));

    [Fact]
    public void The_card_is_named_the_way_the_dashboard_names_it()
    {
        Assert.Equal("Notes", CardAtAGlance.Of(WidgetCard.Notes, Nothing, Morning, English()).Heading);
        Assert.Equal("Upcoming", CardAtAGlance.Of(WidgetCard.Upcoming, Nothing, Morning, English()).Heading);
    }

    [Fact]
    public void A_card_with_nothing_on_it_says_so_rather_than_drawing_an_empty_box()
    {
        var card = CardAtAGlance.Of(WidgetCard.Notes, Nothing, Morning, English());

        Assert.Empty(card.Lines);
        Assert.Equal("Nothing on this yet", card.Message);
    }

    [Fact]
    public void Notes_are_the_most_recently_changed_first_with_how_long_ago()
    {
        var older = NoteCalled("Shopping", Morning.AddDays(-3));
        var newer = NoteCalled("Packing", Morning.AddHours(-2));

        var card = CardAtAGlance.Of(
            WidgetCard.Notes, Nothing with { Notes = [older, newer] }, Morning, English());

        Assert.Equal(["Packing", "Shopping"], card.Lines.Select(line => line.What));
        Assert.NotEmpty(card.Lines[0].Detail);
    }

    /// <summary>Four rows, and the rest left off - the same four every widget here shows.</summary>
    [Fact]
    public void No_more_than_four_rows_fit()
    {
        var notes = Enumerable.Range(0, 7).Select(index => NoteCalled($"Note {index}", Morning)).ToList();

        Assert.Equal(4, CardAtAGlance.Of(WidgetCard.Notes, Nothing with { Notes = notes }, Morning, English()).Lines.Count);
    }

    [Fact]
    public void A_task_list_says_how_far_through_it_is_and_opens_on_the_phone()
    {
        var list = ListCalled("Trip", Entry("Book it", done: true), Entry("Pack", done: false));

        var line = Assert.Single(
            CardAtAGlance.Of(WidgetCard.Tasks, Nothing with { TaskLists = [list] }, Morning, English()).Lines);

        Assert.Equal("Trip", line.What);
        Assert.Equal("1/2", line.Detail);
        Assert.Equal($"/tasks/{list.ServerId}", line.Url);
    }

    /// <summary>
    /// A list this phone made and has not managed to send has no server id, and the paths a tap travels
    /// through are the server's - so it opens Orbit rather than somewhere that cannot be found.
    /// </summary>
    [Fact]
    public void A_list_that_has_never_been_sent_still_opens_the_app()
    {
        var neverSent = ListCalled("Trip", Entry("Pack", done: false));
        neverSent.ServerId = null;

        Assert.Equal(
            string.Empty,
            Assert.Single(CardAtAGlance.Of(
                WidgetCard.Tasks, Nothing with { TaskLists = [neverSent] }, Morning, English()).Lines).Url);
    }

    /// <summary>
    /// What is coming up, soonest first, and nothing that has already been and gone - which is the
    /// whole of what the dashboard's own Upcoming card is for.
    /// </summary>
    [Fact]
    public void Upcoming_is_soonest_first_and_leaves_out_what_is_over()
    {
        var over = Appointment("Stand-up", Morning.AddHours(-2));
        var soon = Appointment("Dentist", Morning.AddHours(4));
        var later = Appointment("Review", Morning.AddDays(3));

        var card = CardAtAGlance.Of(
            WidgetCard.Upcoming, Nothing with { Events = [later, over, soon] }, Morning, English());

        Assert.Equal(["Dentist", "Review"], card.Lines.Select(line => line.What));
        Assert.Equal("/calendar", card.Url);
    }

    /// <summary>
    /// And says the day and the time. Caught on a home screen: written as one custom format ("d MMM, t")
    /// it read "24 Sep, A", because in a custom format string "t" is the first letter of the AM
    /// designator rather than the short time.
    /// </summary>
    [Fact]
    public void An_appointment_says_the_day_and_the_hour()
    {
        var soon = Appointment("Dentist", Morning.AddHours(4));

        var line = Assert.Single(
            CardAtAGlance.Of(WidgetCard.Upcoming, Nothing with { Events = [soon] }, Morning, English()).Lines);

        Assert.StartsWith("1 Sep, ", line.Detail);
        Assert.Contains("12", line.Detail);
        Assert.DoesNotContain("1 Sep, A", line.Detail);
    }

    /// <summary>
    /// A widget placed and never answered for - which a launcher can leave behind, since Android places
    /// one with a configuration activity straight away and defers the question to a tap. It must not
    /// read as "nobody is signed in", which would send somebody to open an app they are signed into.
    /// </summary>
    [Fact]
    public void A_widget_nobody_has_answered_for_asks_rather_than_blaming_the_account()
    {
        var card = CardAtAGlance.NotChosenYet(English());

        Assert.Empty(card.Lines);
        Assert.Equal("Tap to choose what this shows", card.Message);
    }

    [Fact]
    public void A_shelf_says_how_much_is_on_it()
    {
        var shelf = new LocalInventory
        {
            LocalId = Guid.NewGuid(), ServerId = Guid.NewGuid(), Name = "Pantry",
            UpdatedAtUtc = Morning, Items = [Product("Flour"), Product("Rice")]
        };

        var line = Assert.Single(
            CardAtAGlance.Of(WidgetCard.Inventory, Nothing with { Inventories = [shelf] }, Morning, English()).Lines);

        Assert.Equal("Pantry", line.What);
        Assert.Equal("Items: 2", line.Detail);
        Assert.Equal($"/inventory/{shelf.ServerId}", line.Url);
    }

    /// <summary>
    /// The rule that matters most out here. A home screen is on show to whoever is holding the phone,
    /// and the gate that guards private things inside the app has no equivalent on it - so they are
    /// left off every card rather than hidden behind something.
    /// </summary>
    [Fact]
    public void Nothing_private_or_sealed_is_named_on_any_card()
    {
        var privateNote = NoteCalled("Presents", Morning);
        privateNote.IsPrivate = true;
        var sealedNote = NoteCalled("Also presents", Morning);
        sealedNote.IsSealed = true;
        var putAway = NoteCalled("Last year", Morning);
        putAway.IsArchived = true;

        var privateList = ListCalled("Presents", Entry("Order it", done: false));
        privateList.IsPrivate = true;
        var privateShelf = new LocalInventory { LocalId = Guid.NewGuid(), Name = "Cellar", IsPrivate = true };
        var privatePlace = new LocalPlace { LocalId = Guid.NewGuid(), Name = "Home", IsPrivate = true };

        var held = Nothing with
        {
            Notes = [privateNote, sealedNote, putAway],
            TaskLists = [privateList],
            Inventories = [privateShelf],
            Places = [privatePlace]
        };

        Assert.Empty(CardAtAGlance.Of(WidgetCard.Notes, held, Morning, English()).Lines);
        Assert.Empty(CardAtAGlance.Of(WidgetCard.Tasks, held, Morning, English()).Lines);
        Assert.Empty(CardAtAGlance.Of(WidgetCard.Inventory, held, Morning, English()).Lines);
        Assert.Empty(CardAtAGlance.Of(WidgetCard.Places, held, Morning, English()).Lines);
    }

    /// <summary>A place its owner has opened is named, which is the other half of the rule above.</summary>
    [Fact]
    public void A_place_that_is_not_private_is_named()
    {
        var open = new LocalPlace
        {
            LocalId = Guid.NewGuid(), Name = "The allotment", Address = "Field Lane",
            IsPrivate = false, UpdatedAtUtc = Morning
        };

        var line = Assert.Single(
            CardAtAGlance.Of(WidgetCard.Places, Nothing with { Places = [open] }, Morning, English()).Lines);

        Assert.Equal("The allotment", line.What);
        Assert.Equal("Field Lane", line.Detail);
        Assert.Equal("/map", line.Url);
    }

    [Fact]
    public void A_phone_nobody_is_signed_in_on_shows_nothing_at_all()
    {
        var card = CardAtAGlance.ForNobodySignedIn(English());

        Assert.Empty(card.Lines);
        Assert.Empty(card.Heading);
        Assert.Equal("Open Orbit to see this", card.Message);
    }

    private static WhatThePhoneHolds Nothing => WhatThePhoneHolds.Nothing;

    private static Translations English() => new(new InMemoryLanguageStore());

    private static DateTimeOffset Local(DateTime moment)
        => new(moment, TimeZoneInfo.Local.GetUtcOffset(moment));

    private static LocalNote NoteCalled(string title, DateTimeOffset changed)
        => new()
        {
            LocalId = Guid.NewGuid(), ServerId = Guid.NewGuid(), Title = title, UpdatedAtUtc = changed
        };

    private static LocalTaskList ListCalled(string title, params TaskItemDto[] items)
        => new()
        {
            LocalId = Guid.NewGuid(), ServerId = Guid.NewGuid(), Title = title, Items = items,
            UpdatedAtUtc = Morning
        };

    private static TaskItemDto Entry(string description, bool done)
        => new(Guid.NewGuid(), description, null, done, null, "None", false, "None", new TimeOnly(9, 0));

    private static Orbit.Contracts.Inventories.InventoryItemRequest Product(string name)
        => new(Guid.NewGuid(), name, "", "", 1, null, "Piece", null, "None");

    private static LocalCalendarEvent Appointment(string title, DateTimeOffset start)
        => new()
        {
            LocalId = Guid.NewGuid(),
            ServerId = Guid.NewGuid(),
            Details = new CalendarEventDetailsDto(
                title, null, null, null, start.ToUniversalTime(), start.AddHours(1).ToUniversalTime(),
                false, null, [], [], ReminderNotificationChannel: "None")
        };
}
