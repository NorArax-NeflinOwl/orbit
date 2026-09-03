using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Inventories;
using Orbit.Core.Inventories.AcquireInventoryLock;
using Orbit.Core.Inventories.ReleaseInventoryLock;
using Orbit.Core.Notes;
using Orbit.Core.Notes.AcquireNoteLock;
using Orbit.Core.Notes.ReleaseNoteLock;
using Orbit.Core.Users;
using Xunit;

namespace Orbit.Api.Tests.Notes;

/// <summary>
/// The same rule the task list learned the hard way (see TaskListLockTests): holding something open is
/// not a change to it, so a lock is written on its own. Here it saves rewriting a note's whole text,
/// and an inventory's every shelf row, every twenty seconds for as long as a page is open.
/// </summary>
public sealed class NoteAndInventoryLockTests
{
    private readonly InMemoryUserRepository _userRepository = new();
    private readonly Guid _ownerId;

    public NoteAndInventoryLockTests()
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
    public async Task Holding_a_inventory_open_writes_the_lock_and_leaves_the_shelf_alone()
    {
        var repository = new InMemoryInventoryRepository();
        var inventory = Inventory.Create(_ownerId, "Pantry");
        await repository.AddAsync(inventory, CancellationToken.None);
        var resolver = new InventoryAccessResolver(repository, new InMemoryInventoryShareRepository(), _userRepository);

        var outcome = await new AcquireInventoryLockCommandHandler(resolver, repository, _userRepository)
            .HandleAsync(new AcquireInventoryLockCommand(_ownerId, inventory.Id), CancellationToken.None);

        Assert.Equal(EditOutcomeKind.Success, outcome.Kind);
        Assert.Equal(1, repository.LockSaves);
        Assert.Equal(_ownerId, inventory.LockedByUserId);
    }

    [Fact]
    public async Task Letting_a_inventory_go_writes_the_lock_on_its_own_too()
    {
        var repository = new InMemoryInventoryRepository();
        var inventory = Inventory.Create(_ownerId, "Pantry");
        await repository.AddAsync(inventory, CancellationToken.None);
        var resolver = new InventoryAccessResolver(repository, new InMemoryInventoryShareRepository(), _userRepository);
        await new AcquireInventoryLockCommandHandler(resolver, repository, _userRepository)
            .HandleAsync(new AcquireInventoryLockCommand(_ownerId, inventory.Id), CancellationToken.None);

        await new ReleaseInventoryLockCommandHandler(resolver, repository)
            .HandleAsync(new ReleaseInventoryLockCommand(_ownerId, inventory.Id), CancellationToken.None);

        Assert.Equal(2, repository.LockSaves);
        Assert.Null(inventory.LockedByUserId);
    }
}
