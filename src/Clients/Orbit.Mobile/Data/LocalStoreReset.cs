using Microsoft.EntityFrameworkCore;

namespace Orbit.Mobile.Data;

/// <summary>
/// Empties the phone's database when it stops belonging to the person holding it.
///
/// Everything Orbit caches locally - notes, task lists, the calendar, warehouses, contacts, groups and
/// <b>decrypted chat messages</b> - survives a sign-out otherwise, and the next account to sign in on
/// the same phone reads all of it. Found by signing out and signing in as somebody else: the dashboard
/// showed the previous account's notes, and the server had none of them.
///
/// Every table is named explicitly, one delete each, the same way Orbit.Api's account deletion does it.
/// The cost is the same too: <b>a table added later is missed unless it is added here</b>, so this is a
/// thing to check whenever OrbitLocalDbContext grows a DbSet.
/// </summary>
public sealed class LocalStoreReset
{
    private readonly IDbContextFactory<OrbitLocalDbContext> _dbContextFactory;

    public LocalStoreReset(IDbContextFactory<OrbitLocalDbContext> dbContextFactory)
        => _dbContextFactory = dbContextFactory;

    /// <summary>
    /// Throws it all away and records who the empty database now belongs to. Also clears the sync
    /// cursors: keeping them would have the next account ask "what changed since" a moment that has
    /// nothing to do with them, and quietly receive nothing.
    /// </summary>
    public async Task ClearForAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        await dbContext.Notes.ExecuteDeleteAsync(cancellationToken);
        await dbContext.TaskLists.ExecuteDeleteAsync(cancellationToken);
        await dbContext.CalendarEvents.ExecuteDeleteAsync(cancellationToken);
        await dbContext.Warehouses.ExecuteDeleteAsync(cancellationToken);
        await dbContext.Outbox.ExecuteDeleteAsync(cancellationToken);
        await dbContext.SyncCursors.ExecuteDeleteAsync(cancellationToken);
        await dbContext.ChatMessages.ExecuteDeleteAsync(cancellationToken);
        await dbContext.OutgoingChatMessages.ExecuteDeleteAsync(cancellationToken);
        await dbContext.Contacts.ExecuteDeleteAsync(cancellationToken);
        await dbContext.ChatGroups.ExecuteDeleteAsync(cancellationToken);
        await dbContext.StoreOwners.ExecuteDeleteAsync(cancellationToken);
        await dbContext.Permissions.ExecuteDeleteAsync(cancellationToken);

        dbContext.StoreOwners.Add(new LocalStoreOwner { UserId = userId });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Clears the database unless it already belongs to this account. Called on every sign-in, which
    /// covers the case a sign-out cannot: a session that expired, leaving the reader at the sign-in
    /// screen with somebody else's data still cached behind it.
    /// </summary>
    public async Task ClearIfSomebodyElsesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        // Single rather than First, because there is exactly one row or none - see LocalStoreOwner.Id,
        // which is always 1 and is the key. First reads as "any of them", which had EF warning on every
        // sign-in that the query could return whichever row it liked, on a table that has only one.
        var owner = await dbContext.StoreOwners.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (owner?.UserId == userId)
        {
            return;
        }

        await ClearForAsync(userId, cancellationToken);
    }
}
