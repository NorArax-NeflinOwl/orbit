using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Inventory;
using Orbit.Core.Inventory.AcquireWarehouseLock;
using Orbit.Core.Inventory.ReleaseWarehouseLock;
using Orbit.Core.Notes;
using Orbit.Core.Notes.AcquireNoteLock;
using Orbit.Core.Notes.ReleaseNoteLock;
using Orbit.Core.Users;
using Xunit;

namespace Orbit.Api.Tests.Notes;

/// <summary>
/// The same rule the task list learned the hard way (see TaskListLockTests): holding something open is
/// not a change to it, so a lock is written on its own. Here it saves rewriting a note's whole text,
/// and a warehouse's every shelf row, every twenty seconds for as long as a page is open.
/// </summary>
public sealed class NoteAndWarehouseLockTests
{
    private readonly InMemoryUserRepository _userRepository = new();
    private readonly Guid _ownerId;

    public NoteAndWarehouseLockTests()
    {
        var owner = User.Create("owner@example.com", "owner", "Owner", "hash");
        _ownerId = owner.Id;
        _userRepository.AddAsync(owner, CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task Holding_a_note_open_writes_the_lock_and_leaves_the_note_alone()
    {
        var repository = new InMemoryNoteRepository();
        var note = Note.Create(_ownerId, "Shopping", [new NoteContentLine("Milk", IsChecklistItem: false, IsChecked: false)]);
        await repository.AddAsync(note, CancellationToken.None);
        var resolver = new NoteAccessResolver(repository, new InMemoryNoteShareRepository(), _userRepository);

        var outcome = await new AcquireNoteLockCommandHandler(resolver, repository, _userRepository)
            .HandleAsync(new AcquireNoteLockCommand(_ownerId, note.Id), CancellationToken.None);

        Assert.Equal(EditOutcomeKind.Success, outcome.Kind);
        Assert.Equal(1, repository.LockSaves);
        Assert.Equal(_ownerId, note.LockedByUserId);
        Assert.Equal(["Milk"], note.Content.Select(line => line.Text));
    }

    [Fact]
    public async Task Letting_a_note_go_writes_the_lock_on_its_own_too()
    {
        var repository = new InMemoryNoteRepository();
        var note = Note.Create(_ownerId, "Shopping", [new NoteContentLine("Milk", IsChecklistItem: false, IsChecked: false)]);
        await repository.AddAsync(note, CancellationToken.None);
        var resolver = new NoteAccessResolver(repository, new InMemoryNoteShareRepository(), _userRepository);
        await new AcquireNoteLockCommandHandler(resolver, repository, _userRepository)
            .HandleAsync(new AcquireNoteLockCommand(_ownerId, note.Id), CancellationToken.None);

        await new ReleaseNoteLockCommandHandler(resolver, repository)
            .HandleAsync(new ReleaseNoteLockCommand(_ownerId, note.Id), CancellationToken.None);

        Assert.Equal(2, repository.LockSaves);
        Assert.Null(note.LockedByUserId);
    }

    [Fact]
    public async Task Holding_a_warehouse_open_writes_the_lock_and_leaves_the_shelf_alone()
    {
        var repository = new InMemoryWarehouseRepository();
        var warehouse = Warehouse.Create(_ownerId, "Pantry");
        await repository.AddAsync(warehouse, CancellationToken.None);
        var resolver = new WarehouseAccessResolver(repository, new InMemoryWarehouseShareRepository(), _userRepository);

        var outcome = await new AcquireWarehouseLockCommandHandler(resolver, repository, _userRepository)
            .HandleAsync(new AcquireWarehouseLockCommand(_ownerId, warehouse.Id), CancellationToken.None);

        Assert.Equal(EditOutcomeKind.Success, outcome.Kind);
        Assert.Equal(1, repository.LockSaves);
        Assert.Equal(_ownerId, warehouse.LockedByUserId);
    }

    [Fact]
    public async Task Letting_a_warehouse_go_writes_the_lock_on_its_own_too()
    {
        var repository = new InMemoryWarehouseRepository();
        var warehouse = Warehouse.Create(_ownerId, "Pantry");
        await repository.AddAsync(warehouse, CancellationToken.None);
        var resolver = new WarehouseAccessResolver(repository, new InMemoryWarehouseShareRepository(), _userRepository);
        await new AcquireWarehouseLockCommandHandler(resolver, repository, _userRepository)
            .HandleAsync(new AcquireWarehouseLockCommand(_ownerId, warehouse.Id), CancellationToken.None);

        await new ReleaseWarehouseLockCommandHandler(resolver, repository)
            .HandleAsync(new ReleaseWarehouseLockCommand(_ownerId, warehouse.Id), CancellationToken.None);

        Assert.Equal(2, repository.LockSaves);
        Assert.Null(warehouse.LockedByUserId);
    }
}
