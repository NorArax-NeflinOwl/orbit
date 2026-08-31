using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Orbit.Contracts.Notifications;
using Orbit.Contracts.Sync;
using Orbit.Contracts.PushNotifications;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// Orbit's notification endpoints, in memory. Keeps the server's own distinction between read and
/// cleared: clearing tidies an entry out of the recent feed but leaves it on the history, which is
/// exactly the behaviour a client is easy to get wrong about.
/// </summary>
internal sealed class FakeNotificationServer : HttpMessageHandler
{
    private readonly List<NotificationEntryDto> _entries = [];
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public bool IsUnreachable { get; set; }

    /// <summary>Set to make every request come back refused, which is not the same as unreachable.</summary>
    public HttpStatusCode? RefuseEverythingWith { get; set; }

    public NotificationSettingsDto Settings { get; set; } =
        new(AllowNotifications: true, AllowPush: true, AllowEmail: false, AllowMobileBanner: true,
            ShowExceptionDetails: false, BannerVisibleSeconds: 5, BannerMinimumGapSeconds: 2);

    /// <summary>Device tokens registered through /api/push/device-subscriptions, newest last.</summary>
    public List<DevicePushSubscriptionRequest> RegisteredDevices { get; } = [];

    /// <summary>The paths passed to /read-at, in order - what the client claims the reader has now seen.</summary>
    public List<string> MarkedReadAt { get; } = [];

    public IReadOnlyList<NotificationEntryDto> Entries => _entries;

    public NotificationEntryDto Add(string title, string? url, bool isRead = false, bool isDismissed = false)
    {
        var entry = new NotificationEntryDto(
            Guid.NewGuid(), "ChatMessage", title, $"{title} body", url, DateTimeOffset.UtcNow, isRead, isDismissed);
        _entries.Add(entry);
        return entry;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (IsUnreachable)
        {
            throw new HttpRequestException("No such host is known.");
        }

        if (RefuseEverythingWith is { } refusal)
        {
            return new HttpResponseMessage(refusal);
        }

        var path = request.RequestUri!.AbsolutePath;

        if (path.EndsWith("/device-subscriptions", StringComparison.Ordinal))
        {
            RegisteredDevices.Add(
                (await request.Content!.ReadFromJsonAsync<DevicePushSubscriptionRequest>(_json, cancellationToken))!);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        if (path.EndsWith("/notifications/settings", StringComparison.Ordinal))
        {
            if (request.Method == HttpMethod.Put)
            {
                var update = (await request.Content!.ReadFromJsonAsync<UpdateNotificationSettingsRequest>(
                    _json, cancellationToken))!;
                Settings = new NotificationSettingsDto(
                    update.AllowNotifications, update.AllowPush, update.AllowEmail, update.AllowMobileBanner,
                    update.ShowExceptionDetails, update.BannerVisibleSeconds, update.BannerMinimumGapSeconds,
                    update.AllowShareNotifications, update.RetentionDays);
            }

            return Json(Settings);
        }

        // The delta a phone keeps its own copy from. Everything held, every time: what matters to a test
        // is what the client does with an answer, not that the "since" arithmetic is reproduced here.
        if (path.EndsWith("/notifications/changes", StringComparison.Ordinal))
        {
            return Json(new ChangeFeedDto<NotificationEntryDto>(
                _entries, [], DateTimeOffset.UtcNow.UtcDateTime.ToString("O")));
        }

        if (path.EndsWith("/notifications/history", StringComparison.Ordinal))
        {
            return Json(_entries);
        }

        if (path.EndsWith("/notifications/unread", StringComparison.Ordinal))
        {
            return Json(_entries.Where(entry => !entry.IsRead).ToList());
        }

        if (path.EndsWith("/notifications/read-at", StringComparison.Ordinal))
        {
            var read = (await request.Content!.ReadFromJsonAsync<MarkNotificationsReadAtUrlRequest>(
                _json, cancellationToken))!;
            MarkedReadAt.Add(read.Url);
            Replace(entry => entry.Url == read.Url, entry => entry with { IsRead = true });
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        if (path.EndsWith("/notifications/read", StringComparison.Ordinal))
        {
            Replace(_ => true, entry => entry with { IsRead = true });
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        if (path.EndsWith("/notifications", StringComparison.Ordinal))
        {
            if (request.Method == HttpMethod.Delete)
            {
                Replace(_ => true, entry => entry with { IsDismissed = true });
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            // The recent feed leaves out what has been cleared away; the history above does not.
            return Json(_entries.Where(entry => !entry.IsDismissed).ToList());
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private void Replace(Func<NotificationEntryDto, bool> matches, Func<NotificationEntryDto, NotificationEntryDto> replacement)
    {
        for (var index = 0; index < _entries.Count; index++)
        {
            if (matches(_entries[index]))
            {
                _entries[index] = replacement(_entries[index]);
            }
        }
    }

    private HttpResponseMessage Json<TBody>(TBody body)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(body, options: _json) };

    public HttpClient ToHttpClient() => new(this, disposeHandler: false) { BaseAddress = new Uri("https://orbit.example/") };
}
