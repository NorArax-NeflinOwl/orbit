using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Orbit.Contracts.Notifications;

namespace Orbit.Mobile.Data;

/// <summary>
/// The notification feed as this phone holds it - see <see cref="LocalNotification"/>.
///
/// Hands back the same DTO the server would, so the screen above it does not know or care which side
/// the answer came from. Mostly read-only: notifications are the server's to write, and the two actions
/// on them - reading, clearing - go to the server and come back through the delta.
///
/// The exceptions are the few this phone raises for itself (see <see cref="LocalNotification.IsRaisedHere"/>)
/// and the local half of reading and clearing, which has to happen here too: the server cannot mark
/// read something it has never heard of.
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

    /// <summary>
    /// Writes down something only this phone knows - a change the server refused, a copy waiting to be
    /// decided on. Takes the English sentence and what fills its holes rather than finished words, the
    /// same shape the server sends, so the feed says it in the reader's language either way.
    /// </summary>
    public async Task<Guid> RaiseAsync(
        string kind, string title, string body, string? url, DateTimeOffset raisedAtUtc,
        IReadOnlyList<string>? bodyArguments = null, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var notification = new LocalNotification
        {
            Id = Guid.NewGuid(),
            Kind = kind,
            Title = title,
            Body = body,
            Url = url,
            CreatedAtUtc = raisedAtUtc,
            BodyArgumentsJson = JsonSerializer.Serialize(bodyArguments ?? []),
            IsRaisedHere = true
        };

        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync(cancellationToken);
        return notification.Id;
    }

    /// <summary>
    /// Marks everything held read, here on the phone. The feed asks the server too, but a notification
    /// this phone raised is unknown there - left to the server alone, "mark all read" would leave one
    /// stubbornly unread and nothing the reader did would clear it.
    /// </summary>
    public Task MarkEverythingReadAsync(CancellationToken cancellationToken = default)
        => ChangeEachAsync(_ => true, notification => notification.IsRead = true, cancellationToken);

    /// <inheritdoc cref="MarkEverythingReadAsync"/>
    public Task MarkReadAsync(Guid id, CancellationToken cancellationToken = default)
        => ChangeEachAsync(
            notification => notification.Id == id, notification => notification.IsRead = true, cancellationToken);

    /// <summary>Takes everything out of the panel, here on the phone - see MarkEverythingReadAsync.</summary>
    public Task DismissEverythingAsync(CancellationToken cancellationToken = default)
        => ChangeEachAsync(
            _ => true,
            notification =>
            {
                notification.IsDismissed = true;
                notification.IsRead = true;
            },
            cancellationToken);

    /// <summary>
    /// Takes away what this phone raised about one thing, by the destination it points at - so a copy
    /// that has been decided on stops being announced, rather than sitting in the feed forever.
    /// </summary>
    public async Task WithdrawRaisedAtAsync(string url, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var raised = await dbContext.Notifications
            .Where(notification => notification.IsRaisedHere && notification.Url == url)
            .ToListAsync(cancellationToken);

        dbContext.Notifications.RemoveRange(raised);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ChangeEachAsync(
        Func<LocalNotification, bool> wanted, Action<LocalNotification> change, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        foreach (var notification in (await dbContext.Notifications.ToListAsync(cancellationToken)).Where(wanted))
        {
            change(notification);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

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
