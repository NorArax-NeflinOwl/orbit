using Orbit.Core.Inventory;
using Orbit.Core.Users;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// The whole in-memory collaborator graph an inventory handler needs, wired the same way DI wires the
/// real one. Inventory grew a lot of collaborators once items moved under warehouses (access resolution,
/// warehouse lookups, restock tasks), and every test needs the same handful - building them here keeps
/// each test file down to the one handler it actually exercises.
/// </summary>
internal sealed class InventoryTestContext
{
    public InMemoryInventoryRepository InventoryRepository { get; } = new();
    public InMemoryWarehouseRepository WarehouseRepository { get; } = new();
    public InMemoryWarehouseShareRepository WarehouseShareRepository { get; } = new();
    public InMemoryTaskRepository TaskRepository { get; } = new();
    public InMemoryInventoryManagedTaskListRepository ManagedTaskListRepository { get; } = new();
    public InMemoryUserRepository UserRepository { get; } = new();

    public WarehouseAccessResolver AccessResolver { get; }
    public PendingRestockTaskResolver RestockTaskResolver { get; }
    public InventoryTaskListCoordinator TaskListCoordinator { get; }

    /// <summary>Settles finished restock errands against the shelf - see RestockCompletion.</summary>
    public RestockCompletion RestockCompletion { get; }

    public InventoryTestContext()
    {
        AccessResolver = new WarehouseAccessResolver(WarehouseRepository, WarehouseShareRepository, UserRepository);
        RestockTaskResolver = new PendingRestockTaskResolver(TaskRepository, WarehouseRepository);
        TaskListCoordinator = new InventoryTaskListCoordinator(
            TaskRepository, ManagedTaskListRepository, WarehouseRepository, InventoryRepository, RestockTaskResolver);
        RestockCompletion = new RestockCompletion(
            ManagedTaskListRepository, InventoryRepository, WarehouseRepository, TaskRepository);
    }

    /// <summary>Creates and stores a warehouse owned by ownerUserId, returning its id - the starting point for almost every inventory test.</summary>
    public Guid AddWarehouse(Guid ownerUserId, string name = "Kitchen")
    {
        var warehouse = Warehouse.Create(ownerUserId, name);
        WarehouseRepository.AddAsync(warehouse, CancellationToken.None).GetAwaiter().GetResult();
        return warehouse.Id;
    }

    /// <summary>Registers a user, needed by the paths that stamp a name onto something (the edit lock records who holds it).</summary>
    public void AddUser(Guid userId, string userName)
    {
        var user = User.FromPersistence(
            userId, $"{userName}@example.com", userName, userName, "hash", DateTimeOffset.UtcNow, publicKeyBase64: null);
        UserRepository.AddAsync(user, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>Grants recipientUserId already-accepted access to warehouseId, for the tests that exercise a share recipient's view.</summary>
    public void AddAcceptedShare(Guid warehouseId, Guid ownerUserId, Guid recipientUserId, Orbit.Core.Abstractions.ShareAccessLevel accessLevel)
    {
        var share = WarehouseShare.Create(warehouseId, ownerUserId, recipientUserId, accessLevel);
        share.MarkAccepted();
        WarehouseShareRepository.AddAsync(share, CancellationToken.None).GetAwaiter().GetResult();
    }
}
