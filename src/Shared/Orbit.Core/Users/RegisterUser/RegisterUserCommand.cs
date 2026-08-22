using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.RegisterUser;

[ClientAction(ClientActionCategory.Registration)]
public sealed record RegisterUserCommand(string Email, string UserName, string DisplayName, string Password)
    : IRequest<RegisterUserResult>;
