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

        var ownedInventoryIds = await _dbContext.Inventories
            .Where(inventory => inventory.UserId == userId)
            .Select(inventory => inventory.Id)
            .ToListAsync(cancellationToken);
        if (ownedInventoryIds.Count > 0)
        {
            await _dbContext.InventoryItems
                .Where(item => ownedInventoryIds.Contains(item.InventoryId))
                .ExecuteDeleteAsync(cancellationToken);
        }

        await _dbContext.Inventories.Where(inventory => inventory.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Notes.Where(note => note.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        // After the notes and before nothing in particular: a folder holds no rows of its own, so
        // whatever was filed in it is already gone by the time this runs.
        await _dbContext.Folders.Where(folder => folder.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        // The lists a place belonged to go with it through the cascade on that relationship, the same
        // way a task list's entries do - see OrbitDbContext.
        await _dbContext.Places.Where(place => place.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Tasks.Where(task => task.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.CalendarEvents.Where(calendarEvent => calendarEvent.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.RefreshTokens.Where(refreshToken => refreshToken.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.PushSubscriptions.Where(subscription => subscription.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.NotificationSettings.Where(settings => settings.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.NotificationEntries.Where(entry => entry.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.DiagnosticLogEntries.Where(entry => entry.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.SyncTombstones.Where(tombstone => tombstone.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.UserPermissions.Where(permission => permission.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        // Normally already gone: DeleteAccountCommandHandler takes the account out of its groups through
        // the domain first, so an emptied group is removed and a group left without its only admin gets a
        // new one. Swept here too because this method's contract is "every row this account owns", and a
        // membership that outlived its account silently breaks group messaging for everyone still in it.
        await _dbContext.ChatGroupMembers.Where(member => member.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.UserVerificationCodes.Where(code => code.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await DeleteWhatTheAccountHandedOutAsync(userId, cancellationToken);
        await _dbContext.Users.Where(user => user.Id == userId).ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// The account's own rows that name it by something other than a UserId column: the shares it
    /// granted, its contact list, its public links and the positions it shared. They were left behind
    /// because the list above deletes by UserId, and a deletion somebody asked for should take what the
    /// account gave out as well as what it kept - a public link to a note that is gone, a position still
    /// sitting in somebody's map, a contact list of a person who no longer exists.
    ///
    /// Only the account's side of each: a share *to* this account, a contact list naming it, a position
    /// shared *with* it are other people's rows, and IAccountDeletionRepository says why those stay.
    /// </summary>
    private async Task DeleteWhatTheAccountHandedOutAsync(Guid userId, CancellationToken cancellationToken)
    {
        await _dbContext.NoteShares.Where(share => share.OwnerUserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.TaskShares.Where(share => share.OwnerUserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.CalendarEventShares.Where(share => share.OwnerUserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.InventoryShares.Where(share => share.OwnerUserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.PlaceShares.Where(share => share.OwnerUserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Contacts.Where(contact => contact.OwnerUserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.PublicShareLinks.Where(link => link.OwnerUserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.SharedLocations.Where(shared => shared.SharerUserId == userId).ExecuteDeleteAsync(cancellationToken);
    }
}
