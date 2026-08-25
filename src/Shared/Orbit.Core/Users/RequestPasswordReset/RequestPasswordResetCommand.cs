using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.RequestPasswordReset;

/// <summary>
/// Emails a password-reset code, but only to an account whose address is already verified - a reset sent
/// to an unproven address would hand the account to whoever actually reads that mailbox.
///
/// Returns nothing about whether the account exists or qualifies: the endpoint answers the same way
/// regardless, so this can't be used to discover which addresses have accounts.
/// </summary>
public sealed record RequestPasswordResetCommand(string EmailOrUserName) : IRequest<bool>;
