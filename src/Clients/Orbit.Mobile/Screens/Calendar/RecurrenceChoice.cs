using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Screens.Calendar;

/// <summary>
/// One of the ways an event can repeat, paired with what to call it. The picker needs an object with a
/// readable name on it; the wire needs Orbit.Core's own word - see RecurrenceDto - and those are not
/// the same string once the interface is in Polish.
/// </summary>
public sealed record RecurrenceChoice(string Value, string Name)
{
    /// <summary>Orbit.Core's four, in its own order - see RecurrenceFrequency.</summary>
    public static IReadOnlyList<RecurrenceChoice> All(Translations translations)
        =>
        [
            new("Daily", translations["Daily"]),
            new("Weekly", translations["Weekly"]),
            new("Monthly", translations["Monthly"]),
            new("Yearly", translations["Yearly"])
        ];
}
