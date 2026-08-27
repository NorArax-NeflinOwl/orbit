using Orbit.Core.Abstractions;

namespace Orbit.Core.Permissions.RedeemPermissionCode;

/// <summary>Grants whatever permission the typed code unlocks, if it unlocks one and nothing is missing first.</summary>
[ClientAction(ClientActionCategory.Edit)]
public sealed record RedeemPermissionCodeCommand(Guid UserId, string Code) : IRequest<RedeemPermissionCodeOutcome>;

/// <param name="Granted">What was unlocked, or null when nothing was.</param>
/// <param name="MissingPrerequisite">
/// Set when the code was a real one but something has to be unlocked before it - see
/// PermissionPrerequisites. Told apart from a code that matches nothing because the two need different
/// answers: one is "that isn't a code", the other is "not yet, and here is what first".
/// </param>
public sealed record RedeemPermissionCodeOutcome(ApplicationPermission? Granted, ApplicationPermission? MissingPrerequisite);
