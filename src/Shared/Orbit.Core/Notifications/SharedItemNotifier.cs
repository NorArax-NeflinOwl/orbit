using Microsoft.Extensions.Logging;
using Orbit.Core.Users;

namespace Orbit.Core.Notifications;

/// <summary>Tells someone something has been shared with them - see <see cref="SharedItemNotifier"/>.</summary>
public interface ISharedItemNotifier
{
    Task NotifyAsync(
        Guid recipientUserId, Guid sharerUserId, SharedItemKind kind, string? itemTitle, SharedItemLink link,
        CancellationToken cancellationToken);
}

/// <summary>
/// Tells someone that a note, task list, calendar event, inventory, or position has been shared with
/// them. Every Share*CommandHandler goes through here, so being invited to something looks the same
/// whatever kind of thing it is.
///
/// The entry in the notification feed **is** the invitation, so it is always recorded (subject only to
/// the master AllowNotifications switch). Push and email are the extra on top, and only go out on the
/// channels the recipient asked for - see NotificationSettings.ChannelForShares, whose
/// AllowShareNotifications starts off.
/// </summary>
public sealed class SharedItemNotifier : ISharedItemNotifier
{
    private readonly INotificationSettingsRepository _notificationSettingsRepository;
    private readonly NotificationRecorder _notificationRecorder;
    private readonly PushNotificationDispatcher _pushNotificationDispatcher;
    private readonly IUserRepository _userRepository;
    private readonly IEmailSender _emailSender;
    private readonly IWebClientLinks _webClientLinks;
    private readonly ILogger<SharedItemNotifier> _logger;

    public SharedItemNotifier(
        INotificationSettingsRepository notificationSettingsRepository,
        NotificationRecorder notificationRecorder,
        PushNotificationDispatcher pushNotificationDispatcher,
        IUserRepository userRepository,
        IEmailSender emailSender,
        IWebClientLinks webClientLinks,
        ILogger<SharedItemNotifier> logger)
    {
        _notificationSettingsRepository = notificationSettingsRepository;
        _notificationRecorder = notificationRecorder;
        _pushNotificationDispatcher = pushNotificationDispatcher;
        _userRepository = userRepository;
        _emailSender = emailSender;
        _webClientLinks = webClientLinks;
        _logger = logger;
    }

    /// <summary>
    /// Best-effort by design: the share itself has already happened by the time this runs, so a failure
    /// to announce it must not turn a successful share into a failed request. Callers treat it that way.
    /// </summary>
    public async Task NotifyAsync(
        Guid recipientUserId, Guid sharerUserId, SharedItemKind kind, string? itemTitle, SharedItemLink link,
        CancellationToken cancellationToken)
    {
        var sharer = await _userRepository.GetByIdAsync(sharerUserId, cancellationToken);
        var sharerName = sharer?.DisplayName ?? "Someone";
        var settings = await _notificationSettingsRepository.GetAsync(recipientUserId, cancellationToken);

        // One sentence per kind rather than "{0} shared {1} with you" with "a note" dropped in: a
        // language that declines its nouns cannot have the middle of a sentence handed to it as a
        // word, and a translator given five whole sentences can write five that read properly.
        var payload = new PushNotificationPayload(
            TitleFor(kind), [sharerName],
            // The body is what was shared, which is the reader's own words - or, when the thing has no
            // name yet, the heading again rather than a blank line.
            string.IsNullOrWhiteSpace(itemTitle) ? TitleFor(kind) : "{0}",
            [string.IsNullOrWhiteSpace(itemTitle) ? sharerName : itemTitle],
            UrlFor(kind, sharerUserId, link));

        var result = await _notificationRecorder.RecordAndFilterAsync(
            recipientUserId, settings.ChannelForShares(), NotificationEntryKind.SharedWithYou,
            payload, cancellationToken);

        if (result.AllowedChannel.HasFlag(NotificationChannel.Push))
        {
            await _pushNotificationDispatcher.NotifyUserAsync(recipientUserId, payload, cancellationToken);
        }

        if (result.AllowedChannel.HasFlag(NotificationChannel.Email))
        {
            await EmailTheInvitationAsync(recipientUserId, sharerUserId, kind, sharerName, itemTitle, link, cancellationToken);
        }
    }

    /// <summary>
    /// Caught on its own rather than left to the caller: by the time this runs the share is saved and
    /// the invitation is already in the recipient's feed, so an unreachable mail server must not turn
    /// a share that happened into a request that failed. PushNotificationDispatcher above never throws
    /// for the same reason.
    /// </summary>
    private async Task EmailTheInvitationAsync(
        Guid recipientUserId, Guid sharerUserId, SharedItemKind kind, string sharerName, string? itemTitle,
        SharedItemLink link, CancellationToken cancellationToken)
    {
        try
        {
            // Read here rather than alongside the sharer: share notifications start off, so on the
            // common path this account is never looked up at all.
            var recipient = await _userRepository.GetByIdAsync(recipientUserId, cancellationToken);
            if (recipient is null)
            {
                return;
            }

            var itemUrl = _webClientLinks.For(UrlFor(kind, sharerUserId, link));
            var (subject, body) = SharedItemEmailContent.Build(kind, sharerName, itemTitle, itemUrl);
            await _emailSender.SendAsync(recipient.Email, subject, body, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception, "Failed to email a {Kind} share invitation to user {RecipientUserId}", kind, recipientUserId);
        }
    }

    private static string TitleFor(SharedItemKind kind) => kind switch
    {
        SharedItemKind.Note => "{0} shared a note with you",
        SharedItemKind.TaskList => "{0} shared a task list with you",
        SharedItemKind.CalendarEvent => "{0} shared an event with you",
        SharedItemKind.Inventory => "{0} shared an inventory with you",
        _ => "{0} shared their location with you"
    };

    /// <summary>
    /// Where the notification takes the recipient - see <see cref="SharedItemLink"/> for the three
    /// states a share arrives in. It used to lead to the conversation with whoever sent it, because
    /// that is where Accept lived; it leads to what was shared now, and the invitation page is what
    /// carries the Accept for something not yet taken up.
    ///
    /// The sharer's id travels in the path as well as the share's. The web page names them without a
    /// lookup, and the phone - which reads a closed set of paths and has no invitation screen - takes
    /// that last segment and opens the conversation, which is exactly where it landed before.
    /// </summary>
    private static string UrlFor(SharedItemKind kind, Guid sharerUserId, SharedItemLink link)
        => link.PendingShareId is { } shareId
            ? $"/invitation/{PathFor(kind)}/{shareId}/{sharerUserId}"
            : link.ItemId is { } itemId
                ? $"{SectionFor(kind)}/{itemId}"
                : "/map";

    /// <summary>
    /// What each kind is called in a path. Lower case and stable: these strings are in notification rows
    /// already written and in paths already handed to a phone, so renaming one orphans them - the same
    /// rule SharedItemType follows (see OL_PS_ITEMTYPE).
    /// </summary>
    private static string PathFor(SharedItemKind kind) => kind switch
    {
        SharedItemKind.Note => "note",
        SharedItemKind.TaskList => "tasklist",
        SharedItemKind.CalendarEvent => "event",
        SharedItemKind.Inventory => "inventory",
        _ => "location"
    };

    /// <summary>Where one of these is read once it is the recipient's - the client's own routes.</summary>
    private static string SectionFor(SharedItemKind kind) => kind switch
    {
        SharedItemKind.Note => "/notes",
        SharedItemKind.TaskList => "/tasks",
        SharedItemKind.CalendarEvent => "/calendar",
        SharedItemKind.Inventory => "/inventory",
        _ => "/map"
    };
}

/// <summary>What was shared - decides the wording and where the notification leads.</summary>
public enum SharedItemKind
{
    Note,
    TaskList,
    CalendarEvent,
    Inventory,
    Location
}
