using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.SearchUser;

public sealed record SearchUserQuery(Guid RequestingUserId, string Identifier) : IRequest<User?>;
