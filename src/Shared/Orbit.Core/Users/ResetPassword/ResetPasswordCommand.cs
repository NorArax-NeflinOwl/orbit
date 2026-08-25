using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.ResetPassword;

/// <summary>False when the account, code, or its state doesn't check out - deliberately indistinguishable cases.</summary>
public sealed record ResetPasswordCommand(string EmailOrUserName, string Code, string NewPassword) : IRequest<bool>;
