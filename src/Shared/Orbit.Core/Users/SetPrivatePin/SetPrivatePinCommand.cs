using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.SetPrivatePin;

/// <summary>
/// Sets, changes or takes away the PIN a client asks for before it shows what is private - see
/// <see cref="User.PrivatePinHash"/>. Asked for on 2026-09-20.
///
/// **Proved with the account's password**, not with the PIN it is replacing. A PIN is four digits
/// somebody may well have forgotten, and a thing that can only be changed by somebody who still
/// remembers it is a thing that locks its own owner out; the password is the answer that already means
/// "this is my account", and it is the same proof changing the password itself takes.
/// </summary>
/// <param name="NewPin">The PIN to set, or null to take it away entirely.</param>
[ClientAction(ClientActionCategory.Edit)]
public sealed record SetPrivatePinCommand(Guid UserId, string Password, string? NewPin) : IRequest<bool>;
