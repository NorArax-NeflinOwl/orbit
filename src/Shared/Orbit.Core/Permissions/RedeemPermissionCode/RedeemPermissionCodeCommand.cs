using Orbit.Core.Abstractions;

namespace Orbit.Core.Permissions.RedeemPermissionCode;

/// <summary>Grants whatever permission the typed code unlocks, if it unlocks one.</summary>
[ClientAction(ClientActionCategory.Edit)]
public sealed record RedeemPermissionCodeCommand(Guid UserId, string Code) : IRequest<ApplicationPermission?>;
