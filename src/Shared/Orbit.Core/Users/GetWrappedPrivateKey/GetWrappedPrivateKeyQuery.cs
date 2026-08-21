using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.GetWrappedPrivateKey;

public sealed record GetWrappedPrivateKeyQuery(Guid UserId) : IRequest<WrappedPrivateKey?>;
