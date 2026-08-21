using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.SetEncryptionKey;

public sealed record SetEncryptionKeyCommand(Guid UserId, string PublicKeyBase64, WrappedPrivateKey WrappedPrivateKey) : IRequest<bool>;
