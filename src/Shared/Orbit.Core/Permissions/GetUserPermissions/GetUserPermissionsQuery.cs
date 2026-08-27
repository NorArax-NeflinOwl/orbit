using Orbit.Core.Abstractions;

namespace Orbit.Core.Permissions.GetUserPermissions;

/// <summary>What this account has been granted - the source for the Permissions tab and the client's own gating.</summary>
public sealed record GetUserPermissionsQuery(Guid UserId) : IRequest<IReadOnlyList<ApplicationPermission>>;
