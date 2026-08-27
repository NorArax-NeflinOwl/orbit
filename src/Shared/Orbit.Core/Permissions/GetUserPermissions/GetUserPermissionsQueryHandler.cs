using Orbit.Core.Abstractions;

namespace Orbit.Core.Permissions.GetUserPermissions;

public sealed class GetUserPermissionsQueryHandler : IRequestHandler<GetUserPermissionsQuery, IReadOnlyList<ApplicationPermission>>
{
    private readonly IUserPermissionRepository _userPermissionRepository;

    public GetUserPermissionsQueryHandler(IUserPermissionRepository userPermissionRepository)
    {
        _userPermissionRepository = userPermissionRepository;
    }

    public async Task<IReadOnlyList<ApplicationPermission>> HandleAsync(
        GetUserPermissionsQuery request, CancellationToken cancellationToken)
    {
        var granted = await _userPermissionRepository.GetForUserAsync(request.UserId, cancellationToken);
        // Ordered by the enum rather than by when each was granted, so the list reads the same way every
        // time it is shown.
        return [.. Enum.GetValues<ApplicationPermission>().Where(granted.Contains)];
    }
}
