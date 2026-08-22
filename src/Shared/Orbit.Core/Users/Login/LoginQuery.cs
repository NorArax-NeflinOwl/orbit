using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.Login;

[ClientAction(ClientActionCategory.Login)]
public sealed record LoginQuery(string EmailOrUserName, string Password) : IRequest<User?>;
