using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.DeleteAccount;

/// <param name="GoogleIdToken">
/// A Google sign-in made a moment ago, as the other way to prove it is the owner - see
/// DeleteAccountCommandHandler. Null for a client that proves it with the password, or with nothing.
/// </param>
public sealed record DeleteAccountCommand(Guid UserId, string Password, string? GoogleIdToken = null) : IRequest<bool>;
