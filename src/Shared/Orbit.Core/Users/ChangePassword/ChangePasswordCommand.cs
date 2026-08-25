using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.ChangePassword;

/// <summary>
/// Changes the password of a signed-in account, proving intent with the current one. Note that the chat
/// key backup is wrapped with the password and has to be re-wrapped afterwards - only the browser can do
/// that, so the client follows this up with OwnEncryptionKeyProvider.RewrapAsync.
/// </summary>
[ClientAction(ClientActionCategory.Edit)]
public sealed record ChangePasswordCommand(Guid UserId, string CurrentPassword, string NewPassword) : IRequest<bool>;
