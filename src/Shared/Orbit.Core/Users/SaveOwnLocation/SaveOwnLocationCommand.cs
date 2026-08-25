using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.SaveOwnLocation;

/// <summary>
/// Records where the caller is, or clears it when Location is null. Only ever the caller's own - there
/// is no command for writing somebody else's, because nothing should be able to.
/// </summary>
[ClientAction(ClientActionCategory.Save)]
public sealed record SaveOwnLocationCommand(Guid UserId, UserLocation? Location) : IRequest<bool>;
