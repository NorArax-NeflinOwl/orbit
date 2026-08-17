using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.GetUserById;

public sealed record GetUserByIdQuery(Guid Id) : IRequest<User?>;
