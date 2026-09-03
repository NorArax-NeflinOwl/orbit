using Orbit.Contracts.Chat;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Chat;

/// <summary>
/// Turns what the server records about somebody joining a group into the line the thread shows - the
/// phone's half of what GroupConversation.razor writes, in the same words.
///
/// Its own type rather than a method on the screen because the wording carries a rule: the history half
/// is said only when history actually arrived. An admin who asked for it and whose device could open
/// nothing has shared nothing, and saying otherwise would promise the group messages nobody can read.
/// </summary>
public static class GroupAnnouncementLine
{
    public static ReadableChatMessage For(
        ChatGroupAnnouncementDto announcement, LocalChatGroup group, Translations translations)
        => new(IsMine: false, Text: null, announcement.AnnouncedAtUtc, IsEdited: false, IsWaitingToSend: false)
        {
            Announcement = Describe(announcement, group, translations),
            SentAt = translations.WhenItHappened(announcement.AnnouncedAtUtc)
        };

    private static string Describe(
        ChatGroupAnnouncementDto announcement, LocalChatGroup group, Translations translations)
    {
        var joined = translations.Format(
            "{0} joined {1}", NameOf(announcement.JoinedUserId, group, translations), group.Name);

        if (!announcement.HistoryShared)
        {
            return joined;
        }

        var shared = translations.Format(
            "{0} shared the conversation so far", NameOf(announcement.AddedByUserId, group, translations));

        return $"{joined} · {shared}";
    }

    /// <summary>
    /// Whoever this is, by the name the group knows them by. Somebody since removed is no longer in the
    /// membership, and the line still has to read as a sentence - so they are "somebody" rather than a
    /// blank or an id.
    /// </summary>
    private static string NameOf(Guid userId, LocalChatGroup group, Translations translations)
        => group.Members.FirstOrDefault(member => member.UserId == userId)?.DisplayName
            ?? translations["Somebody"];
}
