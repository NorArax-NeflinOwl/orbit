using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.DeleteAccount;

public sealed record DeleteAccountCommand(Guid UserId, string Password) : IRequest<bool>;
