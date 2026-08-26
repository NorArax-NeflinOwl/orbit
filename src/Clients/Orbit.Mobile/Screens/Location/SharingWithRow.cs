using Orbit.Mobile.Data;

namespace Orbit.Mobile.Screens.Location;

/// <summary>
/// Somebody the reader is currently sharing their position with. Named rather than shown as an id: the
/// point of this list is recognising who can see where you are.
/// </summary>
public sealed record SharingWithRow(Guid UserId, string DisplayName, bool IsContinuous, DateTimeOffset UpdatedAtUtc)
{
    public string Description => IsContinuous ? "Live · updated {0:g}" : "One-off · shared {0:g}";

    public string Detail => string.Format(Description, UpdatedAtUtc.ToLocalTime());
}
