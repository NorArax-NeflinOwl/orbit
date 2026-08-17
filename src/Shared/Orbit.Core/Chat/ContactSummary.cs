namespace Orbit.Core.Chat;

/// <summary>
/// A contact list entry: the other party's current public profile joined with when this conversation
/// was last active. Kept separate from <see cref="Contact"/> itself, which only stores the ids - the
/// profile fields are always read live (see GetContactsQueryHandler) rather than cached on the row.
/// </summary>
public sealed record ContactSummary(Orbit.Core.Users.User User, DateTimeOffset LastMessageAtUtc);
