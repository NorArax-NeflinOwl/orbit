using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.SetPrivacyChoice;

/// <summary>
/// Answers the footer's "Do not share my personal information" - see
/// <see cref="User.KeepsThirdPartiesOut"/> for what it actually turns off.
/// </summary>
[ClientAction(ClientActionCategory.Edit)]
public sealed record SetPrivacyChoiceCommand(Guid UserId, bool KeepsThirdPartiesOut) : IRequest<bool>;
