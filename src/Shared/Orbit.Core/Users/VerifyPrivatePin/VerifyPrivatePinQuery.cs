using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.VerifyPrivatePin;

/// <summary>
/// Whether this is the account's PIN - the question a client asks once before it shows what is private
/// (see <see cref="User.PrivatePinHash"/>). A query rather than a command: nothing about the account
/// changes, and what the answer unlocks lives in whichever client asked.
///
/// No ClientAction category: those tag the log lines somebody scans for what a person did, and this is
/// a question asked repeatedly by a client rather than an action anybody took.
/// </summary>
public sealed record VerifyPrivatePinQuery(Guid UserId, string Pin) : IRequest<bool>;
