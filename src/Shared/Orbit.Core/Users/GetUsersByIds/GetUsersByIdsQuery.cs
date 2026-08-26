using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.GetUsersByIds;

/// <summary>
/// Several users at once. The single-user lookup beside this is fine for one name; a client showing a
/// group's roster needs every member's display name and public key, and asking one at a time is a round
/// trip per member every time that screen opens.
/// </summary>
public sealed record GetUsersByIdsQuery(IReadOnlyCollection<Guid> Ids) : IRequest<IReadOnlyList<User>>;
