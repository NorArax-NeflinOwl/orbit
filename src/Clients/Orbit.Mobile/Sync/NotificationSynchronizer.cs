using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orbit.Contracts.Notifications;
using Orbit.Core.Sync;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;

namespace Orbit.Mobile.Sync;

/// <summary>
/// Keeps this phone's copy of the notification feed in step with the server's.
///
/// The simplest thing on the sync spine, and deliberately: notifications are the server's to write -
/// nothing on a phone raises one - so this pulls and never pushes. There is no outbox, no conflict to
/// resolve and no local id, which is why it does not use OutboxReplay at all.
///
/// Nothing leaves the feed by being deleted, so there are no tombstones either. An entry goes only by
/// outliving the retention window, and this prunes by the same rule rather than being told - see the
/// server's /changes endpoint for why it does not send one row per expired notification.
/// </summary>
public sealed class NotificationSynchronizer
{
    /// <summary>
    /// How long the phone keeps one it has heard nothing about. Comfortably past the longest retention
    /// the server offers, so pruning here only ever catches what the server has already forgotten.
    /// </summary>
    private static readonly TimeSpan KeepFor = TimeSpan.FromDays(400);

    private readonly IDbContextFactory<OrbitLocalDbContext> _dbContextFactory;
    private readonly NotificationsClient _notificationsClient;
    private readonly TimeProvider _timeProvider;
    private readonly SyncGate _syncGate;
    private readonly ILogger<NotificationSynchronizer> _logger;

    public NotificationSynchronizer(
        IDbContextFactory<OrbitLocalDbContext> dbContextFactory, NotificationsClient notificationsClient,
        TimeProvider timeProvider, SyncGate syncGate, ILogger<NotificationSynchronizer> logger)
    {
        _dbContextFactory = dbContextFactory;
        _notificationsClient = notificationsClient;
        _timeProvider = timeProvider;
        _syncGate = syncGate;
        _logger = logger;
    }

    /// <summary>Never throws for being offline - see NoteSynchronizer for why that is a rule here.</summary>
    public Task<SyncResult> SynchroniseAsync(CancellationToken cancellationToken = default)
        => _syncGate.RunAsync(SyncEntityType.NotificationEntry, () => RunAsync(cancellationToken), cancellationToken);

    private async Task<SyncResult> RunAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        try
        {
            var received = await PullChangesAsync(dbContext, cancellationToken);
            return new SyncResult(0, received, 0, 0, ReachedTheServer: true);
        }
        catch (Exception exception) when (SyncFailure.IsWorthRetrying(exception, cancellationToken))
        {
            _logger.LogInformation("Could not reach the server to pull notifications ({Reason})", exception.Message);
            return SyncResult.NeverGotThrough(0);
        }
    }

    private async Task<int> PullChangesAsync(OrbitLocalDbContext dbContext, CancellationToken cancellationToken)
    {
        var cursor = await dbContext.SyncCursors
            .FirstOrDefaultAsync(candidate => candidate.EntityType == SyncEntityType.NotificationEntry, cancellationToken);

        var changes = await _notificationsClient.GetChangesAsync(cursor?.Value, cancellationToken);
        foreach (var entry in changes.Changed)
        {
            await StoreAsync(dbContext, entry, cancellationToken);
        }

        Prune(dbContext);

        if (cursor is null)
        {
            dbContext.SyncCursors.Add(new SyncCursor
            {
                EntityType = SyncEntityType.NotificationEntry,
                Value = changes.Cursor
            });
        }
        else
        {
            cursor.Value = changes.Cursor;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return changes.Changed.Count;
    }

    /// <summary>
    /// Written over rather than added to: the delta carries an entry again whenever it is read or
    /// cleared, and the server's copy is the one that counts either way.
    /// </summary>
    private static async Task StoreAsync(
        OrbitLocalDbContext dbContext, NotificationEntryDto entry, CancellationToken cancellationToken)
    {
        var stored = await dbContext.Notifications
            .FirstOrDefaultAsync(candidate => candidate.Id == entry.Id, cancellationToken);

        if (stored is null)
        {
            stored = new LocalNotification { Id = entry.Id };
            dbContext.Notifications.Add(stored);
        }

        stored.Kind = entry.Kind;
        stored.Title = entry.Title;
        stored.Body = entry.Body;
        stored.Url = entry.Url;
        stored.CreatedAtUtc = entry.CreatedAtUtc;
        stored.IsRead = entry.IsRead;
        stored.IsDismissed = entry.IsDismissed;
        stored.TitleArgumentsJson = JsonSerializer.Serialize(entry.TitleArguments ?? []);
        stored.BodyArgumentsJson = JsonSerializer.Serialize(entry.BodyArguments ?? []);
    }

    private void Prune(OrbitLocalDbContext dbContext)
    {
        var tooOld = _timeProvider.GetUtcNow() - KeepFor;
        var expired = dbContext.Notifications.Local
            .Concat(dbContext.Notifications.AsEnumerable())
            .Where(notification => notification.CreatedAtUtc < tooOld)
            .Distinct()
            .ToList();

        dbContext.Notifications.RemoveRange(expired);
    }
}
