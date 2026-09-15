using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Core.Folders;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Data;

/// <summary>
/// Filing an event and a shelf on the phone, which works the way filing a note does: written here
/// first, queued as its own kind of change because it travels on its own endpoint (see
/// OutboxOperation.File), and sent once the folder itself has reached the server.
/// </summary>
public sealed class EventAndShelfFoldersTests : IDisposable
{
    private readonly LocalStore _localStore = new();
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-09-15T10:00:00Z"));
    private readonly FakeCalendarServer _calendarServer;
    private readonly FakeInventoryServer _inventoryServer;
    private readonly FakeFoldersServer _folderServer;
    private readonly LocalCalendarEventRepository _calendarEvents;
    private readonly LocalInventoryRepository _inventories;
    private readonly LocalFolderRepository _folders;

    public EventAndShelfFoldersTests()
    {
        _calendarServer = new FakeCalendarServer(_clock);
        _inventoryServer = new FakeInventoryServer(_clock);
        _folderServer = new FakeFoldersServer(_clock);
        _calendarEvents = new LocalCalendarEventRepository(_localStore, _clock, FixedNetworkStatus.Online);
        _inventories = new LocalInventoryRepository(
            _localStore, _clock, FixedNetworkStatus.Online, PrivateContent.HoldingAKeyFor(Guid.NewGuid()));
        _folders = new LocalFolderRepository(_localStore, _clock);
    }

    public void Dispose()
    {
        _calendarServer.Dispose();
        _inventoryServer.Dispose();
        _folderServer.Dispose();
        _localStore.Dispose();
    }

    [Fact]
    public async Task An_event_is_filed_here_first_and_the_folder_reaches_the_server()
    {
        var week = await _folders.CreateAsync("This week", FolderScope.Calendar);
        var calendarEvent = await _calendarEvents.CreateAsync(FakeCalendarServer.DetailsFor("Dentist", _clock.GetUtcNow()));
        await SynchroniseAsync();

        await _calendarEvents.FileAsync(calendarEvent.LocalId, week.LocalId);

        Assert.Equal(week.LocalId, (await StoredEventAsync(calendarEvent.LocalId)).FolderId);

        await SynchroniseAsync();

        var stored = Assert.Single(_calendarServer.Events);
        var serverFolderId = (await StoredFolderAsync(week.LocalId)).ServerId;
        Assert.NotNull(serverFolderId);
        Assert.Equal(serverFolderId, stored.FolderId);
    }

    /// <summary>
    /// An event the server has never seen carries its folder on the create instead: there is nothing to
    /// send a filing against yet, which is why nothing queues one.
    /// </summary>
    [Fact]
    public async Task An_event_made_and_filed_offline_reaches_the_server_already_filed()
    {
        var week = await _folders.CreateAsync("This week", FolderScope.Calendar);
        var calendarEvent = await _calendarEvents.CreateAsync(FakeCalendarServer.DetailsFor("Dentist", _clock.GetUtcNow()));
        await _calendarEvents.FileAsync(calendarEvent.LocalId, week.LocalId);

        await SynchroniseAsync();

        var stored = Assert.Single(_calendarServer.Events);
        Assert.Equal((await StoredFolderAsync(week.LocalId)).ServerId, stored.FolderId);
    }

    [Fact]
    public async Task A_shelf_is_filed_the_same_way()
    {
        var kitchen = await _folders.CreateAsync("Kitchen", FolderScope.Inventories);
        var inventory = await _inventories.CreateAsync("Pantry");
        await SynchroniseAsync();

        await _inventories.FileAsync(inventory.LocalId, kitchen.LocalId);
        await SynchroniseAsync();

        var stored = Assert.Single(_inventoryServer.Inventories);
        Assert.Equal((await StoredFolderAsync(kitchen.LocalId)).ServerId, stored.FolderId);
    }

    /// <summary>
    /// Deleting a folder empties it rather than taking what was in it - on the phone as on the server,
    /// and for every kind that can be filed.
    /// </summary>
    [Fact]
    public async Task Deleting_a_folder_here_unfiles_the_events_and_shelves_in_it()
    {
        var work = await _folders.CreateAsync("Work", FolderScope.Calendar);
        var calendarEvent = await _calendarEvents.CreateAsync(FakeCalendarServer.DetailsFor("Dentist", _clock.GetUtcNow()));
        var inventory = await _inventories.CreateAsync("Pantry");
        await _calendarEvents.FileAsync(calendarEvent.LocalId, work.LocalId);
        await _inventories.FileAsync(inventory.LocalId, work.LocalId);

        await _folders.DeleteAsync(work.LocalId);

        Assert.Null((await StoredEventAsync(calendarEvent.LocalId)).FolderId);
        Assert.Null((await StoredInventoryAsync(inventory.LocalId)).FolderId);
    }

    /// <summary>
    /// What the server says an event is filed under is named by the server's own id, and is kept here by
    /// this phone's - a folder this phone has not heard of leaves it unfiled rather than pointing at
    /// nothing. See NoteSynchronizer, which says why.
    /// </summary>
    [Fact]
    public async Task A_folder_the_phone_has_never_heard_of_leaves_an_event_unfiled()
    {
        var onTheServer = _calendarServer.AddEvent("Dentist");
        _calendarServer.ReplaceForTest(onTheServer with { FolderId = Guid.NewGuid() });

        await SynchroniseAsync();

        await using var dbContext = _localStore.CreateDbContext();
        Assert.Null(dbContext.CalendarEvents.Single().FolderId);
    }

    private async Task SynchroniseAsync()
    {
        var gate = new SyncGate();
        await new FolderSynchronizer(
            _localStore, new FoldersClient(_folderServer.ToHttpClient()), _clock, gate,
            NullLogger<FolderSynchronizer>.Instance).SynchroniseAsync();
        await new CalendarEventSynchronizer(
            _localStore, new CalendarClient(_calendarServer.ToHttpClient()), _clock, gate,
            new PendingCalendarLinkResolver(_clock, NullLogger<PendingCalendarLinkResolver>.Instance),
            NullLogger<CalendarEventSynchronizer>.Instance).SynchroniseAsync();
        await new InventorySynchronizer(
            _localStore, new InventoryClient(_inventoryServer.ToHttpClient()), _clock, gate,
            NullLogger<InventorySynchronizer>.Instance).SynchroniseAsync();
    }

    private async Task<LocalCalendarEvent> StoredEventAsync(Guid localId)
    {
        await using var dbContext = _localStore.CreateDbContext();
        return dbContext.CalendarEvents.Single(stored => stored.LocalId == localId);
    }

    private async Task<LocalInventory> StoredInventoryAsync(Guid localId)
    {
        await using var dbContext = _localStore.CreateDbContext();
        return dbContext.Inventories.Single(stored => stored.LocalId == localId);
    }

    private async Task<LocalFolder> StoredFolderAsync(Guid localId)
    {
        await using var dbContext = _localStore.CreateDbContext();
        return dbContext.Folders.Single(stored => stored.LocalId == localId);
    }
}
