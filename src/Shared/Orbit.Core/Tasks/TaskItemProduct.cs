using Orbit.Core.Inventories;
using Orbit.Core.Notifications;

namespace Orbit.Core.Tasks;

/// <summary>
/// The product an inventory entry describes, while there is no shelf item to describe it instead.
///
/// An entry of <see cref="TaskItemKind.Inventory"/> names something the work needs, and naming it used
/// to be the whole of it: everything else about that thing - what it is counted in, how much is wanted,
/// how long it keeps - could only be said once a storage existed and the entry had been matched to a row
/// on it. So the answer to "how many do I need" had to be given twice, or given after the fact on
/// another screen.
///
/// This is that answer, written on the entry and kept with it, and it is what
/// <see cref="GenerateInventoryFromTaskList.GenerateInventoryFromTaskListCommandHandler"/> builds the
/// shelf from.
///
/// It exists only while <see cref="TaskItem.LinkedInventoryItemId"/> is null - see
/// <see cref="TaskItem.Product"/>. Once the entry stands for a real shelf item, that item is the answer,
/// and a second copy here would be the one that goes stale.
/// </summary>
/// <param name="Quantity">
/// How much of it there already is - the shelf's starting amount. Zero is not a claim that there is
/// none: it is the box nobody filled in, and the lines already crossed off answer instead - see
/// GenerateInventoryFromTaskListCommandHandler.
/// </param>
/// <param name="Categories">
/// What it is filed under, as many words as apply - the same answer a shelf item keeps (see
/// <see cref="InventoryItem.Categories"/>), because this becomes one. Asking for the single truest word
/// is the question a thing that is plainly both has no answer to.
/// </param>
/// <param name="MinimumQuantity">
/// How little is too little, which is what the work needs. Null leaves that to the counting rule: a
/// thing named three times is three of it.
/// </param>
public sealed record TaskItemProduct(
    string ProductType,
    IReadOnlyList<string> Categories,
    decimal Quantity,
    decimal? MinimumQuantity,
    InventoryUnit Unit,
    DateTimeOffset? ExpiryDate,
    NotificationChannel ExpiryNotificationChannel,
    bool IsCheckedRegularly)
{
    /// <summary>
    /// Two descriptions of the same thing are the same description, categories included. Written out
    /// because a record compares a list by reference, so the generated equality answered "different" for
    /// two products filed under the same words - and the words are tidied into a fresh list on the way
    /// in (see TaskItem's constructor), which means even a product compared with itself as stored would
    /// have said no.
    /// </summary>
    public bool Equals(TaskItemProduct? other)
        => other is not null
            && ProductType == other.ProductType
            && Categories.SequenceEqual(other.Categories)
            && Quantity == other.Quantity
            && MinimumQuantity == other.MinimumQuantity
            && Unit == other.Unit
            && ExpiryDate == other.ExpiryDate
            && ExpiryNotificationChannel == other.ExpiryNotificationChannel
            && IsCheckedRegularly == other.IsCheckedRegularly;

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(ProductType);
        foreach (var category in Categories)
        {
            hash.Add(category);
        }

        hash.Add(Quantity);
        hash.Add(MinimumQuantity);
        hash.Add(Unit);
        hash.Add(ExpiryDate);
        hash.Add(ExpiryNotificationChannel);
        hash.Add(IsCheckedRegularly);
        return hash.ToHashCode();
    }

    /// <summary>
    /// What an entry asks for before anybody says otherwise: nothing said about any of it, counted one
    /// by one. Every blank here falls back to what generating a shelf from a list has always done with a
    /// line nobody filled in - see GenerateInventoryFromTaskListCommandHandler.
    /// </summary>
    public static readonly TaskItemProduct Default = new(
        ProductType: string.Empty, Categories: [], Quantity: 0, MinimumQuantity: null, InventoryUnit.Piece,
        ExpiryDate: null, NotificationChannel.None, IsCheckedRegularly: false);
}
