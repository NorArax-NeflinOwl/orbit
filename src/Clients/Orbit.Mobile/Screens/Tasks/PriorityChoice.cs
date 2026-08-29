using Orbit.Core.Abstractions;
using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Screens.Tasks;

/// <summary>
/// One entry in a list's priority picker - the wire value the API stores, and the word shown while
/// picking. The same shape as <see cref="Inventory.InventoryUnitChoice"/>, and built from the enum
/// rather than listed again, so a priority added there turns up here without anybody remembering.
///
/// The phone could sort its lists by priority from the day it could list them, and had no way to see
/// one or set one: the sort was by something invisible, and the only way to change it was a browser.
/// </summary>
public sealed record PriorityChoice(string Value, string Name)
{
    /// <summary>
    /// Most important first, which is how a picker of three should read - the enum runs the other way
    /// because Low is the least of them, and a list starting at "Low" reads as a recommendation.
    /// </summary>
    public static IReadOnlyList<PriorityChoice> All(Translations translations)
        =>
        [
            .. Enum.GetValues<ItemPriority>()
                .OrderByDescending(priority => priority)
                .Select(priority => new PriorityChoice(priority.ToString(), translations[priority.ToString()]))
        ];

    /// <summary>
    /// Whether this is worth marking on a row at all. Normal is what everything is unless somebody said
    /// otherwise, so a badge on every list would say nothing about any of them - the rule Orbit.Web's
    /// own badge applies.
    /// </summary>
    public bool IsWorthSaying => !string.Equals(Value, nameof(ItemPriority.Normal), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The one whose wire value this is. An unrecognised value reads as Normal, which is what a list
    /// saved by a newer client should look like rather than blank.
    /// </summary>
    public static PriorityChoice For(string value, Translations translations)
    {
        var all = All(translations);
        return all.FirstOrDefault(choice => string.Equals(choice.Value, value, StringComparison.OrdinalIgnoreCase))
            ?? all.Single(choice => choice.Value == nameof(ItemPriority.Normal));
    }
}
