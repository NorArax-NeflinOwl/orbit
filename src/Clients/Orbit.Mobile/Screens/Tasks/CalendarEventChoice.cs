using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Screens.Tasks;

/// <summary>
/// An event a calendar entry can be tied to. Only events the server knows about: the tie is stored as
/// the event's own id, so one that has never synced is not something an entry can point at yet - the
/// same rule <see cref="TaskListChoice"/> follows for moving an entry.
/// </summary>
/// <param name="ServerId">Null for the entry standing for no tie at all.</param>
/// <param name="Address">Where the event says it happens, empty when it says nothing.</param>
public sealed record CalendarEventChoice(Guid? ServerId, string Title, string Address)
{
    /// <summary>Tied to nothing, which is what an entry that holds its own place says.</summary>
    public static CalendarEventChoice NoEvent(Translations translations)
        => new(null, translations["None"], string.Empty);
}
