using Orbit.Core.Calendar;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="ICalendarEventShareRepository"/> stub for unit tests that need real add/lookup/
/// update behavior, including per-recipient scoping, without spinning up SQLite.
/// </summary>
internal sealed class InMemoryCalendarEventShareRepository : ICalendarEventShareRepository
{
    private readonly List<CalendarEventShare> _shares = [];

    public Task AddAsync(CalendarEventShare share, CancellationToken cancellationToken)
    {
        _shares.Add(share);
        return Task.CompletedTask;
    }

    public Task<CalendarEventShare?> GetByIdAsync(Guid recipientUserId, Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_shares.FirstOrDefault(share => share.Id == id && share.RecipientUserId == recipientUserId));

    public Task UpdateAsync(CalendarEventShare share, CancellationToken cancellationToken)
    {
        // Handlers mutate the same CalendarEventShare instance this repository already holds a
        // reference to, so there is nothing to replace here - mirrors InMemoryCalendarEventRepository.
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Guid>> GetAcceptedRecipientUserIdsAsync(Guid sourceCalendarEventId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> recipientUserIds = _shares
            .Where(share => share.SourceCalendarEventId == sourceCalendarEventId && share.IsAccepted)
            .Select(share => share.RecipientUserId)
            .ToList();
        return Task.FromResult(recipientUserIds);
    }
}
