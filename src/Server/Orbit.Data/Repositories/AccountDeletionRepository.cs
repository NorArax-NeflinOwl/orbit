using Microsoft.EntityFrameworkCore;
using Orbit.Core.Users;

namespace Orbit.Data.Repositories;

public sealed class AccountDeletionRepository : IAccountDeletionRepository
{
    private readonly OrbitDbContext _dbContext;

    public AccountDeletionRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Bulk-deletes (via ExecuteDeleteAsync, straight to the database - nothing is loaded into memory
    /// first) every row this account owns, in one transaction so a failure partway through leaves
    /// nothing half-deleted. TaskItemEntity rows go with their parent TaskEntity automatically (see
    /// OrbitDbContext's cascade-delete configuration on that relationship); every other table here has
    /// no such FK, so each is listed explicitly. See IAccountDeletionRepository's class comment for what
    /// this deliberately leaves dangling elsewhere.
    /// </summary>
    public async Task DeleteAllDataForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var ownedWarehouseIds = await _dbContext.Warehouses
            .Where(warehouse => warehouse.UserId == userId)
            .Select(warehouse => warehouse.Id)
            .ToListAsync(cancellationToken);
        if (ownedWarehouseIds.Count > 0)
        {
            await _dbContext.InventoryItems
                .Where(item => ownedWarehouseIds.Contains(item.WarehouseId))
                .ExecuteDeleteAsync(cancellationToken);
        }

        await _dbContext.Warehouses.Where(warehouse => warehouse.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Notes.Where(note => note.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Tasks.Where(task => task.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.CalendarEvents.Where(calendarEvent => calendarEvent.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.RefreshTokens.Where(refreshToken => refreshToken.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.PushSubscriptions.Where(subscription => subscription.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.NotificationSettings.Where(settings => settings.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.NotificationEntries.Where(entry => entry.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        // Normally already gone: DeleteAccountCommandHandler takes the account out of its groups through
        // the domain first, so an emptied group is removed and a group left without its only admin gets a
        // new one. Swept here too because this method's contract is "every row this account owns", and a
        // membership that outlived its account silently breaks group messaging for everyone still in it.
        await _dbContext.ChatGroupMembers.Where(member => member.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.UserVerificationCodes.Where(code => code.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Users.Where(user => user.Id == userId).ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }
}
