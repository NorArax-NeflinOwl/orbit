using Orbit.Mobile.Localization;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens;

/// <summary>
/// The line under a row saying why it cannot be changed right now, or that a change is still on its
/// way out. Notes, task lists, warehouses and calendar events all answer the same two questions - see
/// <see cref="OfflineEditPolicy"/> - so they say it the same way rather than four times over.
/// </summary>
public static class OfflineEditExplanation
{
    /// <summary>Empty when there is nothing worth saying, which is the common case.</summary>
    public static string For(OfflineEditRefusal refusal, bool hasUnsentChanges, Translations translations)
        => refusal switch
        {
            OfflineEditRefusal.SharedWithYou
                => translations["Shared with you - read-only until you're back online"],
            OfflineEditRefusal.SharedWithOthers
                => translations["Shared with others - read-only until you're back online"],
            _ => hasUnsentChanges ? translations["Waiting to sync"] : string.Empty
        };
}
