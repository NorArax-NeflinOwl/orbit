using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.Login;

public sealed record LoginQuery(string EmailOrUserName, string Password) : IRequest<User?>;
