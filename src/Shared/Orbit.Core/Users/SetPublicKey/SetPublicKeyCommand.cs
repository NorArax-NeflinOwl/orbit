using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.SetPublicKey;

public sealed record SetPublicKeyCommand(Guid UserId, string PublicKeyBase64) : IRequest<bool>;
