using System.ComponentModel.DataAnnotations.Schema;
using Orbit.Contracts;
using Orbit.Contracts.Inventory;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Data;

/// <summary>
/// A warehouse as the phone holds it - the fourth and last entity type of phase 4.
///
/// One thing genuinely differs from notes, task lists and calendar events: the change feed describes the
/// warehouse but not what is in it, because <see cref="WarehouseDto"/> carries no items - the API serves
/// those from their own endpoint. So <see cref="Items"/> is filled by a second call, made only for the
/// warehouses a pull actually reported as changed rather than for all of them.
/// </summary>
public sealed class LocalWarehouse : ISharedState, ICopyableForEditing
{
    public Guid LocalId { get; set; }

    /// <summary>The id the server knows this warehouse by. Null until a create has actually been accepted.</summary>
    public Guid? ServerId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <inheritdoc cref="LocalTaskList.Description"/>
    public string Description { get; set; } = string.Empty;

    public IReadOnlyList<WarehouseItemDto> Items { get; set; } = [];

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

    /// <summary>True when the owner shared this warehouse out and another person can change it.</summary>
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
