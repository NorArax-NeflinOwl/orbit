using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.Login;

public sealed record LoginQuery(string Email, string Password) : IRequest<User?>;
