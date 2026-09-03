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
/// <param name="ProductId">
/// Which product on that shelf this errand is about, so the shelf opens on it rather than on sixty rows
/// with no sign of which one was meant. Null for a reference to another list, which points at the list
/// itself.
/// </param>
public sealed record TaskItemReference(
    string Label, Guid LocalId, TaskItemReferenceTarget Target, Guid? ProductId = null);
