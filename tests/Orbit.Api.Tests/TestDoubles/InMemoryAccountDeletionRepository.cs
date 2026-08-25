using Orbit.Core.Users;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// Records which account was wiped rather than actually touching any data - DeleteAccountCommandHandler
/// tests only need to pin down when this is (and isn't) called, not the cross-table deletion mechanics
/// themselves, which live in AccountDeletionRepository's real EF Core implementation.
/// </summary>
internal sealed class InMemoryAccountDeletionRepository : IAccountDeletionRepository
{
    public List<Guid> DeletedUserIds { get; } = [];

    public Task DeleteAllDataForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        DeletedUserIds.Add(userId);
        return Task.CompletedTask;
    }
}
