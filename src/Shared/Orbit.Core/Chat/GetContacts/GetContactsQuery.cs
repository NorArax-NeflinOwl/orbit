using Orbit.Core.Abstractions;

namespace Orbit.Core.Chat.GetContacts;

public sealed record GetContactsQuery(Guid UserId) : IRequest<IReadOnlyList<ContactSummary>>;
