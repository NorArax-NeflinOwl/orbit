using Orbit.Contracts.Calendar;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Data;

/// <summary>
/// A calendar event as the phone holds it. The third entity type on the sync spine, and the one that
/// was meant to be an addition rather than a discovery.
///
/// Everything the event actually is lives in <see cref="Details"/>, which the server treats as one
/// block too - so unlike a task list, there is nothing here to take apart.
/// </summary>
public sealed class LocalCalendarEvent : ISharedState
{
    public Guid LocalId { get; set; }

    /// <summary>The id the server knows this event by. Null until a create has actually been accepted.</summary>
    public Guid? ServerId { get; set; }

    public CalendarEventDetailsDto Details { get; set; } = EmptyDetails;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public bool IsShared { get; set; }

    public string? SharedByUserName { get; set; }

    /// <summary>True when the owner shared this event out and another person can change it.</summary>
    public bool IsSharedWithOthers { get; set; }

    public string AccessLevel { get; set; } = "CanEdit";

    public DateTimeOffset? LastSyncedAtUtc { get; set; }

    /// <summary>
    /// What a row holds before anything has been read into it. EF needs a value it can construct, and an
    /// event with no title and no times is obviously unset rather than subtly wrong.
    /// </summary>
    private static CalendarEventDetailsDto EmptyDetails { get; } = new(
        string.Empty, null, null, null, default, default, false, null, [], [], "None", "None");
}
