using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Orbit.Core.Chat.Groups;
using Orbit.Core.Sync;
using Orbit.Core.Permissions;
using Orbit.Api.Tests.TestDoubles;
using Orbit.Data;
using Orbit.Data.Entities;
using Orbit.Data.Repositories;
using Xunit;

namespace Orbit.Api.Tests.Users;

/// <summary>
/// Exercises the real <see cref="AccountDeletionRepository"/> against a SQLite database rather than the
/// in-memory double the handler tests use, because the bug this guards against lives precisely in the
/// part a double replaces: the repository enumerates every table by hand, and a table left off that list
/// is invisible to any test that stubs it out. ChatGroupMembers was missed exactly that way.
///
/// SQLite stands in for PostgreSQL here for the same reason DatabaseHealthCheckTests uses it - what's
/// being tested is which rows the method decides to delete, not anything provider-specific.
/// </summary>
public sealed class AccountDeletionSweepTests : IDisposable
{
    private readonly TemporarySqliteDatabase _database = new();
    private readonly OrbitDbContext _dbContext;

    public AccountDeletionSweepTests() => _dbContext = _database.DbContext;

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Every_table_holding_the_account_is_emptied()
    {
        var userId = Guid.NewGuid();
        var survivorId = Guid.NewGuid();
        await SeedEverythingOwnedByAsync(userId);
        await SeedEverythingOwnedByAsync(survivorId);

        await new AccountDeletionRepository(_dbContext).DeleteAllDataForUserAsync(userId, CancellationToken.None);

        foreach (var (table, remaining) in await CountRowsPerTableAsync(userId))
        {
            Assert.True(remaining == 0, $"{table} still held {remaining} row(s) belonging to the deleted account");
        }
    }

    [Fact]
    public async Task Another_account_is_left_untouched()
    {
        var userId = Guid.NewGuid();
        var survivorId = Guid.NewGuid();
        await SeedEverythingOwnedByAsync(userId);
        await SeedEverythingOwnedByAsync(survivorId);

        await new AccountDeletionRepository(_dbContext).DeleteAllDataForUserAsync(userId, CancellationToken.None);

        foreach (var (table, remaining) in await CountRowsPerTableAsync(survivorId))
        {
            Assert.True(remaining > 0, $"{table} lost the surviving account's row(s)");
        }
    }

    [Fact]
    public async Task A_group_membership_does_not_outlive_the_account()
    {
        // Called out on its own because of what leaving it behind costs: the server accepts a group
        // message only when there is exactly one ciphertext copy per current member, and nobody can
        // encrypt for an account whose public key is gone - so one stale row silently makes the group
        // unusable for everyone still in it.
        var userId = Guid.NewGuid();
        await SeedEverythingOwnedByAsync(userId);

        await new AccountDeletionRepository(_dbContext).DeleteAllDataForUserAsync(userId, CancellationToken.None);

        Assert.Empty(await _dbContext.ChatGroupMembers.Where(member => member.UserId == userId).ToListAsync());
    }

    [Fact]
    public void Every_entity_owning_a_user_is_covered_by_this_test()
    {
        // The trap that produced this bug: someone adds a table with a UserId column and nothing points
        // out that account deletion now misses it. This fails when that happens, naming the table.
        var ownedByUser = _dbContext.Model.GetEntityTypes()
            .Where(entityType => entityType.ClrType.GetProperty("UserId", BindingFlags.Public | BindingFlags.Instance) is not null)
            .Select(entityType => entityType.ClrType.Name)
            .ToHashSet();

        var missing = ownedByUser.Except(SeededEntityTypeNames).ToList();

        Assert.True(
            missing.Count == 0,
            $"{string.Join(", ", missing)} has a UserId but is not seeded here, so nothing proves account " +
            "deletion removes it. Add it to SeedEverythingOwnedByAsync and to AccountDeletionRepository.");
    }

    /// <summary>Every entity type this test plants a row in - kept beside the seeding so the two can't drift.</summary>
    private static readonly string[] SeededEntityTypeNames =
    [
        nameof(NoteEntity), nameof(TaskEntity), nameof(CalendarEventEntity), nameof(WarehouseEntity),
        nameof(RefreshTokenEntity), nameof(PushSubscriptionEntity), nameof(NotificationSettingsEntity),
        nameof(NotificationEntryEntity), nameof(UserVerificationCodeEntity), nameof(ChatGroupMemberEntity),
        nameof(DiagnosticLogEntryEntity), nameof(SyncTombstoneEntity), nameof(UserPermissionEntity)
    ];

    private async Task SeedEverythingOwnedByAsync(Guid userId)
    {
        var now = DateTimeOffset.UtcNow;
        _dbContext.Notes.Add(new NoteEntity { Id = Guid.NewGuid(), UserId = userId, Title = "Note", ContentJson = "[]", CreatedAtUtc = now, UpdatedAtUtc = now });
        _dbContext.Tasks.Add(new TaskEntity { Id = Guid.NewGuid(), UserId = userId, Title = "Tasks", CreatedAtUtc = now, UpdatedAtUtc = now });
        _dbContext.CalendarEvents.Add(new CalendarEventEntity { Id = Guid.NewGuid(), UserId = userId, Title = "Event", StartUtc = now, EndUtc = now, CreatedAtUtc = now, UpdatedAtUtc = now, Color = "#fff", CreationNotificationChannel = "None", ReminderNotificationChannel = "None", RemindersJson = "[]", GuestsJson = "[]" });
        _dbContext.Warehouses.Add(new WarehouseEntity { Id = Guid.NewGuid(), UserId = userId, Name = "Store", CreatedAtUtc = now, UpdatedAtUtc = now });
        _dbContext.RefreshTokens.Add(new RefreshTokenEntity { Id = Guid.NewGuid(), UserId = userId, TokenHash = Guid.NewGuid().ToString("N"), ExpiresAtUtc = now, CreatedAtUtc = now });
        _dbContext.PushSubscriptions.Add(new PushSubscriptionEntity { Id = Guid.NewGuid(), UserId = userId, Endpoint = $"https://push.example/{userId}", P256dhBase64 = "k", AuthBase64 = "a", CreatedAtUtc = now });
        _dbContext.NotificationSettings.Add(new NotificationSettingsEntity { Id = Guid.NewGuid(), UserId = userId });
        _dbContext.NotificationEntries.Add(new NotificationEntryEntity { Id = Guid.NewGuid(), UserId = userId, Kind = "Chat", Title = "Hi", Body = "Body", CreatedAtUtc = now });
        _dbContext.UserPermissions.Add(new UserPermissionEntity { UserId = userId, Permission = nameof(ApplicationPermission.Contacts), GrantedAtUtc = now });
        _dbContext.UserVerificationCodes.Add(new UserVerificationCodeEntity { Id = Guid.NewGuid(), UserId = userId, Purpose = "EmailVerification", CodeHash = "h", EmailAddress = "a@example.com", CreatedAtUtc = now, ExpiresAtUtc = now });
        // The membership needs a real group to point at - ChatGroupMembers.GroupId is a foreign key.
        var groupId = Guid.NewGuid();
        _dbContext.ChatGroups.Add(new ChatGroupEntity { Id = groupId, Name = "Team", CreatedByUserId = userId, CreatedAtUtc = now });
        _dbContext.ChatGroupMembers.Add(new ChatGroupMemberEntity { Id = Guid.NewGuid(), GroupId = groupId, UserId = userId, Role = nameof(ChatGroupRole.Member), JoinedAtUtc = now });
        _dbContext.DiagnosticLogEntries.Add(new DiagnosticLogEntryEntity { Id = Guid.NewGuid(), UserId = userId, ReceivedAtUtc = now, TimestampUtc = now, Level = "Error", Message = "Something went wrong" });
        _dbContext.SyncTombstones.Add(new SyncTombstoneEntity { Id = Guid.NewGuid(), UserId = userId, EntityType = SyncEntityType.Note, EntityId = Guid.NewGuid(), DeletedAtUtc = now });
        await _dbContext.SaveChangesAsync();
    }

    private async Task<IReadOnlyList<(string Table, int Remaining)>> CountRowsPerTableAsync(Guid userId) =>
    [
        (nameof(_dbContext.Notes), await _dbContext.Notes.CountAsync(row => row.UserId == userId)),
        (nameof(_dbContext.Tasks), await _dbContext.Tasks.CountAsync(row => row.UserId == userId)),
        (nameof(_dbContext.CalendarEvents), await _dbContext.CalendarEvents.CountAsync(row => row.UserId == userId)),
        (nameof(_dbContext.Warehouses), await _dbContext.Warehouses.CountAsync(row => row.UserId == userId)),
        (nameof(_dbContext.RefreshTokens), await _dbContext.RefreshTokens.CountAsync(row => row.UserId == userId)),
        (nameof(_dbContext.PushSubscriptions), await _dbContext.PushSubscriptions.CountAsync(row => row.UserId == userId)),
        (nameof(_dbContext.NotificationSettings), await _dbContext.NotificationSettings.CountAsync(row => row.UserId == userId)),
        (nameof(_dbContext.NotificationEntries), await _dbContext.NotificationEntries.CountAsync(row => row.UserId == userId)),
        (nameof(_dbContext.UserPermissions), await _dbContext.UserPermissions.CountAsync(row => row.UserId == userId)),
        (nameof(_dbContext.UserVerificationCodes), await _dbContext.UserVerificationCodes.CountAsync(row => row.UserId == userId)),
        (nameof(_dbContext.ChatGroupMembers), await _dbContext.ChatGroupMembers.CountAsync(row => row.UserId == userId)),
        (nameof(_dbContext.DiagnosticLogEntries), await _dbContext.DiagnosticLogEntries.CountAsync(row => row.UserId == userId)),
        (nameof(_dbContext.SyncTombstones), await _dbContext.SyncTombstones.CountAsync(row => row.UserId == userId))
    ];
}
