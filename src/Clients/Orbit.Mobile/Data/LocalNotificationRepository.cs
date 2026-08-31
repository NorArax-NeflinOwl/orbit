using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Orbit.Contracts.Notifications;

namespace Orbit.Mobile.Data;

/// <summary>
/// The notification feed as this phone holds it - see <see cref="LocalNotification"/>.
///
/// Hands back the same DTO the server would, so the screen above it does not know or care which side
/// the answer came from. Read-only: notifications are the server's to write, and the two actions on
/// them - reading, clearing - go to the server and come back through the delta.
/// </summary>
public sealed class LocalNotificationRepository
{
    private readonly IDbContextFactory<OrbitLocalDbContext> _dbContextFactory;

    public LocalNotificationRepository(IDbContextFactory<OrbitLocalDbContext> dbContextFactory)
        => _dbContextFactory = dbContextFactory;

    /// <summary>What the panel shows: most recent first, and nothing the reader has cleared away.</summary>
    public Task<IReadOnlyList<NotificationEntryDto>> GetRecentAsync(CancellationToken cancellationToken = default)
        => ReadAsync(notification => !notification.IsDismissed, cancellationToken);

    /// <summary>Everything held, cleared entries included - the notifications page's own view.</summary>
    public Task<IReadOnlyList<NotificationEntryDto>> GetHistoryAsync(CancellationToken cancellationToken = default)
        => ReadAsync(_ => true, cancellationToken);

    /// <summary>The unread ones, which is what badges each place a notification came from.</summary>
    public Task<IReadOnlyList<NotificationEntryDto>> GetUnreadAsync(CancellationToken cancellationToken = default)
        => ReadAsync(notification => !notification.IsRead && !notification.IsDismissed, cancellationToken);

    private async Task<IReadOnlyList<NotificationEntryDto>> ReadAsync(
        Func<LocalNotification, bool> wanted, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var stored = await dbContext.Notifications.AsNoTracking()
            .OrderByDescending(notification => notification.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return [.. stored.Where(wanted).Select(ToDto)];
    }

    private static NotificationEntryDto ToDto(LocalNotification notification)
        => new(
            notification.Id,
            notification.Kind,
            notification.Title,
            notification.Body,
            notification.Url,
            notification.CreatedAtUtc,
            notification.IsRead,
            notification.IsDismissed,
            Read(notification.TitleArgumentsJson),
            Read(notification.BodyArgumentsJson));

    /// <summary>An unreadable list is an empty one: a malformed row must not take the whole feed down.</summary>
    private static IReadOnlyList<string> Read(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
