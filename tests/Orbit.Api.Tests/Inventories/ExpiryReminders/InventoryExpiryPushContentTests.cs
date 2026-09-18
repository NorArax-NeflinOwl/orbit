using Orbit.Core.Inventories.ExpiryReminders;
using Orbit.Core.Notifications;
using Xunit;

namespace Orbit.Api.Tests.Inventories.ExpiryReminders;

/// <summary>
/// What a warning about something going off says, and where it leads. The address matters as much as
/// the words: every page that reads the feed marks what a notification names, so an address that named
/// only the section left the pages able to say something was about to go off and unable to say where.
/// </summary>
public sealed class InventoryExpiryPushContentTests
{
    [Fact]
    public void The_warning_names_the_storage_the_item_is_on()
    {
        var inventoryId = Guid.NewGuid();

        var payload = InventoryExpiryPushContent.Build(AReminder(inventoryId));

        Assert.StartsWith($"/inventory/{inventoryId}?", payload.Url);
    }

    /// <summary>
    /// And the row on it, the way everything in Orbit names a row it wants landed on. Following the
    /// warning opens the shelf with that row picked out, and the shelf marks it - a card saying
    /// "something happened here" over thirty rows still left the reader to find which. Asked for on
    /// 2026-09-18.
    /// </summary>
    [Fact]
    public void And_the_row_it_is_about()
    {
        var reminder = AReminder(Guid.NewGuid());

        var payload = InventoryExpiryPushContent.Build(reminder);

        Assert.Equal(
            $"/inventory/{reminder.InventoryId}?highlight={reminder.InventoryItemId}", payload.Url);
    }

    /// <summary>
    /// Under the section, not beside it: NotificationFeedState matches at a path boundary, so the
    /// dashboard's card - which asks about "/inventory" - is still marked by a warning about one
    /// storage on it.
    /// </summary>
    [Fact]
    public void And_still_counts_as_news_about_the_section()
        => Assert.StartsWith("/inventory/", InventoryExpiryPushContent.Build(AReminder(Guid.NewGuid())).Url);

    [Fact]
    public void It_says_what_is_going_off_and_when()
    {
        var payload = InventoryExpiryPushContent.Build(AReminder(Guid.NewGuid()));

        Assert.Equal("Expiring soon", payload.TitleFormat);
        Assert.Contains("Milk", payload.BodyArguments);
    }

    private static DueExpiryReminder AReminder(Guid inventoryId)
        => new(
            InventoryItemId: Guid.NewGuid(), InventoryId: inventoryId, UserId: Guid.NewGuid(),
            Name: "Milk", ExpiryDate: new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero),
            NotificationChannel: NotificationChannel.Push, Quantity: 1);
}
