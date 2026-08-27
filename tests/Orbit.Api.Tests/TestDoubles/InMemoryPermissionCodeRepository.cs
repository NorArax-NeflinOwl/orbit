using Orbit.Core.Permissions;

namespace Orbit.Api.Tests.TestDoubles;

internal sealed class InMemoryPermissionCodeRepository : IPermissionCodeRepository
{
    private readonly Dictionary<ApplicationPermission, PermissionCode> _codes = [];

    public Task<IReadOnlyList<PermissionCode>> GetAllAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<PermissionCode>>([.. _codes.Values]);

    public Task AddIfAbsentAsync(PermissionCode code, CancellationToken cancellationToken)
    {
        _codes.TryAdd(code.Permission, code);
        return Task.CompletedTask;
    }
}
