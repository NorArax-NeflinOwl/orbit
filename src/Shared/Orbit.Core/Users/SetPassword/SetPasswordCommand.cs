using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.SetPassword;

/// <summary>
/// Sets the first password on an account that has none - a Google account reaching chat, which needs a
/// password to encrypt its chat key backup with. Needs no current password because there isn't one;
/// being signed in is the proof. An account that already has a password must use ChangePasswordCommand.
/// </summary>
[ClientAction(ClientActionCategory.Edit)]
public sealed record SetPasswordCommand(Guid UserId, string NewPassword) : IRequest<bool>;
