using Microsoft.EntityFrameworkCore;
using Orbit.Mobile.Data;

namespace Orbit.Mobile.Sync;

/// <summary>
/// How far this device has caught up for one entity type. The value is opaque here on purpose - it is
/// whatever the server's change feed handed back - so the client never has to agree with the server
/// about what a cursor means.
/// </summary>
public static class SyncCursors
{
    public static async Task<string?> ReadAsync(
        OrbitLocalDbContext dbContext, string entityType, CancellationToken cancellationToken)
        => (await Find(dbContext, entityType, cancellationToken))?.Value;

    /// <summary>Not saved here - the caller commits it with whatever the pull changed, or not at all.</summary>
    public static async Task WriteAsync(
        OrbitLocalDbContext dbContext, string entityType, string value, CancellationToken cancellationToken)
    {
        if (await Find(dbContext, entityType, cancellationToken) is { } existing)
        {
            existing.Value = value;
            return;
        }

        dbContext.SyncCursors.Add(new SyncCursor { EntityType = entityType, Value = value });
    }

    private static Task<SyncCursor?> Find(
        OrbitLocalDbContext dbContext, string entityType, CancellationToken cancellationToken)
        => dbContext.SyncCursors.FirstOrDefaultAsync(cursor => cursor.EntityType == entityType, cancellationToken);
}
