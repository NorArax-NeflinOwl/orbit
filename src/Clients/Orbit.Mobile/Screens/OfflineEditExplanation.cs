using Orbit.Mobile.Localization;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens;

/// <summary>
/// The line under a row, or above an editor, saying why it cannot be changed right now - or that a
/// change is still on its way out. Notes, task lists, inventories and calendar events answer the same
/// questions, so they say it the same way rather than four times over.
///
/// Two reasons, and they are not the same shape. A share that does not permit editing holds whatever
/// the connection is like and is nobody's to wait out - see <see cref="SharedItemAccess"/>. The
/// offline ones are temporary and say so.
/// </summary>
public static class OfflineEditExplanation
{
    /// <summary>Empty when there is nothing worth saying, which is the common case.</summary>
    public static string For(
        ISharedState item, OfflineEditRefusal refusal, bool hasUnsentChanges, Translations translations)
    {
        // First, because it is the one that does not change when the phone reconnects: telling somebody
        // to try again online would be sending them to wait for nothing.
        if (SharedItemAccess.WhyItCannotBeEdited(item, translations) is { Length: > 0 } sharedToRead)
        {
            return sharedToRead;
        }

        return refusal switch
        {
            OfflineEditRefusal.SharedWithYou
                => translations["Shared with you - read-only until you're back online"],
            OfflineEditRefusal.SharedWithOthers
                => translations["Shared with others - read-only until you're back online"],
            _ => hasUnsentChanges ? translations["Waiting to sync"] : string.Empty
        };
    }
}
