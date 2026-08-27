using Orbit.Core.Permissions;

namespace Orbit.Api.Tests.TestDoubles;

internal sealed class InMemoryUserPermissionRepository : IUserPermissionRepository
{
    private readonly Dictionary<Guid, HashSet<ApplicationPermission>> _byUser = [];

    public Task<IReadOnlySet<ApplicationPermission>> GetForUserAsync(Guid userId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlySet<ApplicationPermission>>(_byUser.TryGetValue(userId, out var granted) ? granted : new HashSet<ApplicationPermission>());

    public Task GrantAsync(Guid userId, ApplicationPermission permission, CancellationToken cancellationToken)
    {
        if (!_byUser.TryGetValue(userId, out var granted))
        {
            granted = [];
            _byUser[userId] = granted;
        }

        granted.Add(permission);
        return Task.CompletedTask;
    }
}
