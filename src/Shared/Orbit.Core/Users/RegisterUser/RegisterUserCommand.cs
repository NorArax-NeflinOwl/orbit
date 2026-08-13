using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.RegisterUser;

public sealed record RegisterUserCommand(string Email, string DisplayName, string Password) : IRequest<RegisterUserResult>;
