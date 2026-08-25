using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory;

/// <summary>
/// A named container for inventory items, owned by exactly one user for its entire lifetime - sharing
/// (see WarehouseShare) grants other users access to this same row, it never creates a copy. Items no
/// longer carry an owner of their own: <see cref="InventoryItem.WarehouseId"/> points here, and access
/// to an item is whatever access the caller has to its warehouse.
///
/// <see cref="IsShared"/>/<see cref="SharedByUserName"/>/<see cref="AccessLevel"/> are not persisted:
/// they describe how the *current caller* relates to this warehouse, recomputed on every read by
/// WarehouseAccessResolver via <see cref="SetAccessContext"/> - exactly the shape Note uses.
///
/// Deliberately without the edit-lock fields Note/TaskList/CalendarEvent carry: a warehouse is a name
/// and a bag of items, and its items are edited individually, so there is no long multi-field form here
/// for two people to collide over.
/// </summary>
public sealed class Warehouse
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Name { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>False for the owner, true for anyone reaching this warehouse through a share.</summary>
    public bool IsShared { get; private set; }

    /// <summary>The owner's login, whenever IsShared is true. Null otherwise.</summary>
    public string? SharedByUserName { get; private set; }

    /// <summary>The current caller's access level - always CanEdit for the owner, and whatever their share grants otherwise.</summary>
    public ShareAccessLevel AccessLevel { get; private set; } = ShareAccessLevel.CanEdit;

    private Warehouse(Guid id, Guid userId, string name, DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc)
    {
        Id = id;
        UserId = userId;
        Name = name;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public static Warehouse Create(Guid userId, string name)
    {
        var now = DateTimeOffset.UtcNow;
        return new Warehouse(Guid.NewGuid(), userId, name, now, now);
    }

    /// <summary>Rebuilds a warehouse from already-persisted values, bypassing creation rules.</summary>
    public static Warehouse FromPersistence(Guid id, Guid userId, string name, DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc)
        => new(id, userId, name, createdAtUtc, updatedAtUtc);

    /// <summary>
    /// Stamps how the current caller relates to this warehouse - see the class comment. Called exactly
    /// once, by WarehouseAccessResolver, right after loading the row; never persisted.
    /// </summary>
    public void SetAccessContext(bool isShared, string? sharedByUserName, ShareAccessLevel accessLevel)
    {
        IsShared = isShared;
        SharedByUserName = sharedByUserName;
        AccessLevel = accessLevel;
    }

    /// <summary>Callers are expected to have already checked <see cref="AccessLevel"/> is CanEdit - see UpdateWarehouseCommandHandler.</summary>
    public void Update(string name)
    {
        Name = name;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
