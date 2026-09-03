using Orbit.Contracts.Chat;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Chat;

/// <summary>
/// The line a group's thread shows when somebody joins - the phone's half of what
/// GroupConversation.razor writes, and the same words.
/// </summary>
public sealed class GroupAnnouncementLineTests
{
    private static readonly Guid JoinedUserId = Guid.NewGuid();
    private static readonly Guid AdminUserId = Guid.NewGuid();

    [Fact]
    public void A_join_names_who_arrived_and_the_group_they_arrived_in()
    {
        var line = Describe(historyShared: false);

        Assert.True(line.IsAnnouncement);
        Assert.Contains("Celina", line.Announcement);
        Assert.Contains("Trip", line.Announcement);
    }

    /// <summary>
    /// The history half is said only when history actually arrived. An admin who asked for it and whose
    /// device could open nothing has shared nothing, and saying otherwise would promise the group
    /// messages nobody can read.
    /// </summary>
    [Fact]
    public void Nothing_is_said_about_the_past_when_none_was_handed_over()
    {
        var line = Describe(historyShared: false);

        Assert.DoesNotContain("shared", line.Announcement);
    }

    [Fact]
    public void Handing_the_past_over_is_said_too_and_names_who_did_it()
    {
        var line = Describe(historyShared: true);

        Assert.Contains("Celina", line.Announcement);
        Assert.Contains("Ada", line.Announcement);
        Assert.Contains("shared the conversation so far", line.Announcement);
    }

    /// <summary>
    /// Somebody since removed is no longer in the membership, and the line still has to read as a
    /// sentence rather than as a blank or an id.
    /// </summary>
    [Fact]
    public void Somebody_no_longer_in_the_group_still_reads_as_a_sentence()
    {
        var line = GroupAnnouncementLine.For(
            new ChatGroupAnnouncementDto(
                Guid.NewGuid(), JoinedUserId: Guid.NewGuid(), AdminUserId, HistoryShared: false,
                DateTimeOffset.Parse("2026-08-30T10:00:00Z")),
            AGroup(),
            new Translations(new InMemoryLanguageStore()));

        Assert.Contains("Somebody", line.Announcement);
        Assert.Contains("Trip", line.Announcement);
    }

    /// <summary>An announcement is not a message, and the row it shares has to say so.</summary>
    [Fact]
    public void An_announcement_is_not_read_as_a_message_that_could_not_be_opened()
    {
        var line = Describe(historyShared: false);

        Assert.False(line.CannotBeOpened);
        Assert.False(line.IsNotAnnouncement);
        Assert.Null(line.Text);
    }

    private static ReadableChatMessage Describe(bool historyShared)
        => GroupAnnouncementLine.For(
            new ChatGroupAnnouncementDto(
                Guid.NewGuid(), JoinedUserId, AdminUserId, historyShared,
                DateTimeOffset.Parse("2026-08-30T10:00:00Z")),
            AGroup(),
            new Translations(new InMemoryLanguageStore()));

    private static LocalChatGroup AGroup()
        => new()
        {
            Id = Guid.NewGuid(),
            Name = "Trip",
            Members =
            [
                new LocalChatGroupMember(JoinedUserId, "Member", "Celina", null),
                new LocalChatGroupMember(AdminUserId, "Admin", "Ada", null)
            ]
        };
}
