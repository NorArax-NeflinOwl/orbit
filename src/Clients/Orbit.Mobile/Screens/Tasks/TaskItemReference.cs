namespace Orbit.Mobile.Screens.Tasks;

/// <summary>What a reference under an errand leads to, which is what tapping it has to open.</summary>
public enum TaskItemReferenceTarget
{
    /// <summary>The warehouse the product sits in.</summary>
    Warehouse,

    /// <summary>Another list asking for the same product.</summary>
    TaskList
}

/// <summary>
/// A place worth going from an inventory errand: the shelf it is about, and every other list asking for
/// the same product.
///
/// Shown as something to tap rather than a label, for the reason Orbit.Web gives about its own chips -
/// the reason to say "also on Weekend" at all is to be able to go and look. Both are worked out from
/// what this phone already holds rather than asked for, so an errand still says where it points with no
/// connection.
/// </summary>
/// <param name="Label">Already in the reader's language - "in Kitchen", "also on Weekend".</param>
/// <param name="LocalId">This phone's id for what to open, which is what the navigator takes.</param>
public sealed record TaskItemReference(string Label, Guid LocalId, TaskItemReferenceTarget Target);
