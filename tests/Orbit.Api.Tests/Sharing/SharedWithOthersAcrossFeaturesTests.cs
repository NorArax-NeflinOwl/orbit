using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Calendar;
using Orbit.Core.Inventory;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks;
using Orbit.Core.Users;
using Xunit;

namespace Orbit.Api.Tests.Sharing;

/// <summary>
/// The owner's side of sharing, for the three features that have now joined notes in carrying it. The
/// rule is the same everywhere: an accepted grant makes an item shared out, a pending offer does not,
/// and the recipient never sees the flag - their side of the relationship is IsShared.
///
/// It exists for the mobile client, which cannot hold an edit lock and so has to treat anything another
/// person can change as read-only while offline (info/orbit-maui-plan.md §5.4). Without it an owner's
/// copy of a shared item is indistinguishable from a private one, and the policy has nothing to act on.
/// One class for all three, because the behaviour is identical and three near-identical classes would
/// hide that rather than show it.
/// </summary>
public sealed class SharedWithOthersAcrossFeaturesTests
{
    private static readonly CalendarEventDetails SomeEvent = new(
        "Title", null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), false, null, [], [],
        CreationNotificationChannel: NotificationChannel.None, ReminderNotificationChannel: NotificationChannel.None);

    private readonly InMemoryUserRepository _users = new();
    private readonly User _owner = User.Create("owner@example.com", "owner", "Owner", "hash");

    public SharedWithOthersAcrossFeaturesTests()
        => _users.AddAsync(_owner, CancellationToken.None).GetAwaiter().GetResult();

    [Fact]
    public async Task An_owner_sees_which_task_lists_are_shared_out()
    {
        var lists = new InMemoryTaskRepository();
        var shares = new InMemoryTaskListShareRepository();

        var shared = TaskList.Create(_owner.Id, "Shared", []);
        var privateOne = TaskList.Create(_owner.Id, "Mine alone", []);
        await lists.AddAsync(shared, CancellationToken.None);
        await lists.AddAsync(privateOne, CancellationToken.None);
        await shares.AddAsync(Accepted(TaskListShare.Create(shared.Id, _owner.Id, Guid.NewGuid())), CancellationToken.None);
        // A pending offer on the second list: nobody can read or change it yet, so it does not count.
        await shares.AddAsync(TaskListShare.Create(privateOne.Id, _owner.Id, Guid.NewGuid()), CancellationToken.None);

        var resolved = await new TaskListAccessResolver(lists, shares, _users).ResolveAllAsync(_owner.Id, updatedSinceUtc: null, CancellationToken.None);

        Assert.True(resolved.Single(list => list.Title == "Shared").IsSharedWithOthers);
        Assert.False(resolved.Single(list => list.Title == "Mine alone").IsSharedWithOthers);
    }

    [Fact]
    public async Task An_owner_sees_which_calendar_events_are_shared_out()
    {
        var events = new InMemoryCalendarEventRepository();
        var shares = new InMemoryCalendarEventShareRepository();

        var shared = CalendarEvent.Create(_owner.Id, SomeEvent with { Title = "Shared" });
        var privateOne = CalendarEvent.Create(_owner.Id, SomeEvent with { Title = "Mine alone" });
        await events.AddAsync(shared, CancellationToken.None);
        await events.AddAsync(privateOne, CancellationToken.None);
        await shares.AddAsync(Accepted(CalendarEventShare.Create(shared.Id, _owner.Id, Guid.NewGuid())), CancellationToken.None);

        var resolved = await new CalendarEventAccessResolver(events, shares, _users).ResolveAllAsync(_owner.Id, updatedSinceUtc: null, CancellationToken.None);

        Assert.True(resolved.Single(item => item.Details.Title == "Shared").IsSharedWithOthers);
        Assert.False(resolved.Single(item => item.Details.Title == "Mine alone").IsSharedWithOthers);
    }

    [Fact]
    public async Task An_owner_sees_which_warehouses_are_shared_out()
    {
        var warehouses = new InMemoryWarehouseRepository();
        var shares = new InMemoryWarehouseShareRepository();

        var shared = Warehouse.Create(_owner.Id, "Shared");
        var privateOne = Warehouse.Create(_owner.Id, "Mine alone");
        await warehouses.AddAsync(shared, CancellationToken.None);
        await warehouses.AddAsync(privateOne, CancellationToken.None);
        await shares.AddAsync(Accepted(WarehouseShare.Create(shared.Id, _owner.Id, Guid.NewGuid())), CancellationToken.None);

        var resolved = await new WarehouseAccessResolver(warehouses, shares, _users).ResolveAllAsync(_owner.Id, updatedSinceUtc: null, CancellationToken.None);

        Assert.True(resolved.Single(item => item.Name == "Shared").IsSharedWithOthers);
        Assert.False(resolved.Single(item => item.Name == "Mine alone").IsSharedWithOthers);
    }

    [Fact]
    public async Task The_recipient_of_a_share_is_never_told_it_is_shared_out()
    {
        var recipientId = Guid.NewGuid();
        var lists = new InMemoryTaskRepository();
        var shares = new InMemoryTaskListShareRepository();

        var shared = TaskList.Create(_owner.Id, "Shared", []);
        await lists.AddAsync(shared, CancellationToken.None);
        await shares.AddAsync(
            Accepted(TaskListShare.Create(shared.Id, _owner.Id, recipientId, ShareAccessLevel.CanEdit)), CancellationToken.None);

        var resolved = await new TaskListAccessResolver(lists, shares, _users).ResolveAllAsync(recipientId, updatedSinceUtc: null, CancellationToken.None);

        // Conflating the two ends of one relationship would make every shared item look shared out to
        // everybody, and the offline policy would then refuse edits nobody else could possibly make.
        var asRecipient = Assert.Single(resolved);
        Assert.True(asRecipient.IsShared);
        Assert.False(asRecipient.IsSharedWithOthers);
    }

    [Fact]
    public async Task The_flag_is_on_a_single_read_too_not_only_the_list()
    {
        var lists = new InMemoryTaskRepository();
        var shares = new InMemoryTaskListShareRepository();

        var shared = TaskList.Create(_owner.Id, "Shared", []);
        await lists.AddAsync(shared, CancellationToken.None);
        await shares.AddAsync(Accepted(TaskListShare.Create(shared.Id, _owner.Id, Guid.NewGuid())), CancellationToken.None);

        var resolved = await new TaskListAccessResolver(lists, shares, _users)
            .ResolveAsync(_owner.Id, shared.Id, CancellationToken.None);

        Assert.True(resolved!.IsSharedWithOthers);
    }

    private static TaskListShare Accepted(TaskListShare share)
    {
        share.MarkAccepted();
        return share;
    }

    private static CalendarEventShare Accepted(CalendarEventShare share)
    {
        share.MarkAccepted();
        return share;
    }

    private static WarehouseShare Accepted(WarehouseShare share)
    {
        share.MarkAccepted();
        return share;
    }
}
