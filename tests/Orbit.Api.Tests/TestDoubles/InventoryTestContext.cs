using Orbit.Core.Inventories;
using Orbit.Core.Inventories.UpdateInventory;
using Orbit.Core.Users;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// The whole in-memory collaborator graph an inventory handler needs, wired the same way DI wires the
/// real one. Inventory grew a lot of collaborators once items moved under inventories (access resolution,
/// inventory lookups, restock tasks), and every test needs the same handful - building them here keeps
/// each test file down to the one handler it actually exercises.
/// </summary>
internal sealed class InventoryTestContext
{
    public InMemoryInventoryItemRepository InventoryItemRepository { get; } = new();
    public InMemoryInventoryRepository InventoryRepository { get; } = new();
    public InMemoryInventoryShareRepository InventoryShareRepository { get; } = new();
    public InMemoryTaskRepository TaskRepository { get; } = new();
    public InMemoryInventoryManagedTaskListRepository ManagedTaskListRepository { get; } = new();
    public InMemoryUserRepository UserRepository { get; } = new();

    public InventoryAccessResolver AccessResolver { get; }
    public PendingRestockTaskResolver RestockTaskResolver { get; }
    public InventoryTaskListCoordinator TaskListCoordinator { get; }

    /// <summary>Settles finished restock errands against the shelf - see RestockCompletion.</summary>
    public RestockCompletion RestockCompletion { get; }

    /// <summary>Rebuilds a restock list against the settings and the shelf - see RestockListRefresh.</summary>
    public RestockListRefresh RestockListRefresh { get; }

    /// <summary>Crosses off the entries a shelf already answers - see StockedEntryCompletion.</summary>
    public StockedEntryCompletion StockedEntryCompletion { get; }

    /// <summary>Writes an inventory's item list - what both creating one and saving one go through.</summary>
    public InventoryItemsSaver ItemsSaver { get; }

    /// <summary>Puts a list's product entries on the shelf it is measured against - see ProductEntryPlacement.</summary>
    public ProductEntryPlacement ProductEntryPlacement { get; }

    /// <summary>Which lists Orbit keeps for itself, so neither count counts one twice - see ManagedRestockLists.</summary>
    public ManagedRestockLists ManagedRestockLists { get; }

    /// <summary>Writes an amount edited on the shelf back onto the entries asking for it - see ShelfDemand.</summary>
    public ShelfDemand ShelfDemand { get; }

    /// <summary>Counts what the lists ask of each shelf item - see ShelfUsage.</summary>
    public ShelfUsage ShelfUsage { get; }

    public InventoryTestContext()
    {
        AccessResolver = new InventoryAccessResolver(InventoryRepository, InventoryShareRepository, UserRepository);
        RestockTaskResolver = new PendingRestockTaskResolver(TaskRepository, InventoryRepository);
        TaskListCoordinator = new InventoryTaskListCoordinator(
            TaskRepository, ManagedTaskListRepository, InventoryRepository, InventoryItemRepository, RestockTaskResolver);
        RestockCompletion = new RestockCompletion(
            ManagedTaskListRepository, InventoryItemRepository, InventoryRepository, TaskRepository);
        RestockListRefresh = new RestockListRefresh(
            ManagedTaskListRepository, InventoryItemRepository, InventoryRepository, TaskRepository, TaskListCoordinator);
        StockedEntryCompletion = new StockedEntryCompletion(InventoryRepository, InventoryItemRepository);
        ItemsSaver = new InventoryItemsSaver(InventoryItemRepository, TaskListCoordinator);
        ProductEntryPlacement = new ProductEntryPlacement(AccessResolver, InventoryItemRepository, RestockListRefresh);
        ManagedRestockLists = new ManagedRestockLists(ManagedTaskListRepository);
        ShelfDemand = new ShelfDemand(TaskRepository, ManagedRestockLists);
        ShelfUsage = new ShelfUsage(TaskRepository, InventoryItemRepository, ManagedRestockLists);
    }

    /// <summary>
    /// A save of an inventory, wired the way DI wires it. Its own method here because the handler has
    /// six collaborators now and every test that saves an inventory needs the same six.
    /// </summary>
    public UpdateInventoryCommandHandler InventorySave()
        => new(AccessResolver, InventoryRepository, InventoryItemRepository, ItemsSaver, ShelfDemand, ShelfUsage);

    /// <summary>Creates and stores an inventory owned by ownerUserId, returning its id - the starting point for almost every inventory test.</summary>
    public Guid AddInventory(Guid ownerUserId, string name = "Kitchen")
    {
        var inventory = Inventory.Create(ownerUserId, name);
        InventoryRepository.AddAsync(inventory, CancellationToken.None).GetAwaiter().GetResult();
        return inventory.Id;
    }

    /// <summary>Registers a user, needed by the paths that stamp a name onto something (the edit lock records who holds it).</summary>
    public void AddUser(Guid userId, string userName)
    {
        var user = User.FromPersistence(
            userId, $"{userName}@example.com", userName, userName, "hash", DateTimeOffset.UtcNow, publicKeyBase64: null);
        UserRepository.AddAsync(user, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>Grants recipientUserId already-accepted access to inventoryId, for the tests that exercise a share recipient's view.</summary>
    public void AddAcceptedShare(Guid inventoryId, Guid ownerUserId, Guid recipientUserId, Orbit.Core.Abstractions.ShareAccessLevel accessLevel)
    {
        var share = InventoryShare.Create(inventoryId, ownerUserId, recipientUserId, accessLevel);
        share.MarkAccepted();
        InventoryShareRepository.AddAsync(share, CancellationToken.None).GetAwaiter().GetResult();
    }
}
