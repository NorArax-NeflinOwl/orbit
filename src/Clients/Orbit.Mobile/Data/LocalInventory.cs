using System.ComponentModel.DataAnnotations.Schema;
using Orbit.Contracts;
using Orbit.Contracts.Inventories;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Data;

/// <summary>
/// An inventory as the phone holds it - the fourth and last entity type of phase 4.
///
/// One thing genuinely differs from notes, task lists and calendar events: the change feed describes the
/// inventory but not what is in it, because <see cref="InventoryDto"/> carries no items - the API serves
/// those from their own endpoint. So <see cref="Items"/> is filled by a second call, made only for the
/// inventories a pull actually reported as changed rather than for all of them.
/// </summary>
public sealed class LocalInventory : ISharedState, ICopyableForEditing
{
    public Guid LocalId { get; set; }

    /// <summary>The id the server knows this inventory by. Null until a create has actually been accepted.</summary>
    public Guid? ServerId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <inheritdoc cref="LocalTaskList.Description"/>
    public string Description { get; set; } = string.Empty;

    public IReadOnlyList<InventoryItemRequest> Items { get; set; } = [];

    /// <summary>
    /// When each batch on this shelf arrived, by its id. Kept beside the items rather than on them: the
    /// item shape is what a save sends back, and the server decides when something arrived - a phone
    /// returning its own answer to that would be returning a guess.
    ///
    /// A row this phone added and has not synced yet is missing from here, and says nothing about when
    /// it arrived, which is honest: nothing has accepted it yet. Two rows of one name are two
    /// deliveries, and this is the only thing that tells them apart - see InventoryItemRow.
    /// </summary>
    public IReadOnlyDictionary<Guid, DateTimeOffset> ItemArrivals { get; set; }
        = new Dictionary<Guid, DateTimeOffset>();

    public bool IsPrivate { get; set; }

    /// <inheritdoc cref="LocalNote.EncryptedCiphertext"/>
    public string? EncryptedCiphertext { get; set; }

    public string? EncryptedNonce { get; set; }

    /// <inheritdoc cref="LocalNote.EncryptedContent"/>
    [NotMapped]
    public EncryptedContentDto? EncryptedContent
        => EncryptedCiphertext is { } ciphertext && EncryptedNonce is { } nonce
            ? new EncryptedContentDto(ciphertext, nonce)
            : null;

    /// <inheritdoc cref="LocalNote.IsSealed"/>
    [NotMapped]
    public bool IsSealed { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public bool IsShared { get; set; }

    public string? SharedByUserName { get; set; }

    /// <summary>True when the owner shared this inventory out and another person can change it.</summary>
    public bool IsSharedWithOthers { get; set; }

    public string AccessLevel { get; set; } = "CanEdit";
    /// <inheritdoc cref="LocalNote.OwnerUserId"/>
    public Guid? OwnerUserId { get; set; }

    public DateTimeOffset? LastSyncedAtUtc { get; set; }

    /// <inheritdoc cref="LocalNote.CopyOfLocalId"/>
    public Guid? CopyOfLocalId { get; set; }

    /// <inheritdoc cref="LocalNote.CopiedAtUtc"/>
    public DateTimeOffset? CopiedAtUtc { get; set; }

    /// <inheritdoc cref="LocalNote.CopyBaseTitle"/>
    public string CopyBaseTitle { get; set; } = string.Empty;

    /// <inheritdoc cref="ICopyableForEditing.CopyBaseLines"/>
    public IReadOnlyList<string> CopyBaseLines { get; set; } = [];

    /// <inheritdoc cref="LocalNote.IsKeptCopy"/>
    public bool IsKeptCopy { get; set; }

}
