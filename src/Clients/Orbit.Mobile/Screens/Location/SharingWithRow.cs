using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Screens.Location;

/// <summary>
/// Somebody the reader is currently sharing their position with. Named rather than shown as an id: the
/// point of this list is recognising who can see where you are.
/// </summary>
/// <param name="Detail">Already in the reader's language, so the row itself needs no dictionary.</param>
public sealed record SharingWithRow(
    Guid UserId, string DisplayName, bool IsContinuous, DateTimeOffset UpdatedAtUtc, string Detail)
{
    public static SharingWithRow From(
        Guid userId, string displayName, bool isContinuous, DateTimeOffset updatedAtUtc,
        Translations translations)
        => new(
            userId, displayName, isContinuous, updatedAtUtc,
            translations.Format(
                isContinuous ? "Live · updated {0}" : "One-off · shared {0}",
                updatedAtUtc.ToLocalTime().ToString("g", translations.DisplayCulture)));
}
