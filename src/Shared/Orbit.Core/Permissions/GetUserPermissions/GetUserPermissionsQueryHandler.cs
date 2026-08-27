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
        // What applies, not what is stored: a permission whose prerequisite is missing lets this account
        // do nothing, and saying otherwise would put a row in the Permissions tab that the gate refuses.
        // Ordered by the enum, so the list reads the same way every time it is shown.
        return PermissionPrerequisites.Effective(granted);
    }
}
