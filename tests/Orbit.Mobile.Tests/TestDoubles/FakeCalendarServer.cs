using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using Orbit.Contracts.Calendar;
using Orbit.Contracts.Sync;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>Orbit's calendar endpoints, in memory.</summary>
internal sealed class FakeCalendarServer : HttpMessageHandler
{
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<Guid, CalendarEventDto> _events = [];
    private readonly List<(Guid Id, DateTimeOffset DeletedAtUtc)> _tombstones = [];

    public FakeCalendarServer(TimeProvider timeProvider) => _timeProvider = timeProvider;

    public List<string> ReceivedRequests { get; } = [];

    public bool IsUnreachable { get; set; }

    /// <summary>
    /// Set to make a request hang until the client gives up, which is what a phone with no route
    /// actually sees - a timeout rather than a refusal. Told apart from IsUnreachable because the two
    /// arrive as different exception types and code has been written that only handled one.
    /// </summary>
    public bool TimesOut { get; set; }

    public IReadOnlyCollection<CalendarEventDto> Events => _events.Values;

    public CalendarEventDto AddEvent(string title, bool isShared = false, bool isSharedWithOthers = false)
    {
        var now = _timeProvider.GetUtcNow();
        var calendarEvent = new CalendarEventDto(
            Guid.NewGuid(), DetailsFor(title, now), now, now,
            isShared, isShared ? "someone" : null, "CanEdit", null, isSharedWithOthers);

        _events[calendarEvent.Id] = calendarEvent;
        return calendarEvent;
    }

    public void DeleteEvent(Guid id)
    {
        _events.Remove(id);
        _tombstones.Add((id, _timeProvider.GetUtcNow()));
    }

    public static CalendarEventDetailsDto DetailsFor(string title, DateTimeOffset startUtc)
        => new(title, null, null, null, startUtc, startUtc.AddHours(1), false, null, [], [], "None", "None");

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        ReceivedRequests.Add($"{request.Method} {path}");

        if (IsUnreachable)
        {
            throw new HttpRequestException("No such host is known.");
        }

        if (TimesOut)
        {
            throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout.");
        }

        // Nobody else is ever in it here; EditLockTests covers the answer where somebody is.
        if (path.EndsWith("/lock", StringComparison.Ordinal))
        {
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        if (path.EndsWith("/changes", StringComparison.Ordinal))
        {
            var since = DateTimeOffset.Parse(HttpUtility.ParseQueryString(request.RequestUri.Query)["since"]!);
            return Json(new ChangeFeedDto<CalendarEventDto>(
                _events.Values.Where(item => item.UpdatedAtUtc >= since).ToList(),
                _tombstones.Where(entry => entry.DeletedAtUtc >= since).Select(entry => entry.Id).ToList(),
                _timeProvider.GetUtcNow().UtcDateTime.ToString("O")));
        }

        return request.Method.Method switch
        {
            "POST" => await CreateAsync(request, cancellationToken),
            "PUT" => await UpdateAsync(request, path, cancellationToken),
            "DELETE" => Delete(path),
            _ => Json(_events.Values.ToList())
        };
    }

    private async Task<HttpResponseMessage> CreateAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = await ReadAsync<CreateCalendarEventRequest>(request, cancellationToken);
        var created = AddEvent(body!.Details.Title);
        _events[created.Id] = created with { Details = ToDto(body.Details) };
        return Json(created.Id, HttpStatusCode.Created);
    }

    private async Task<HttpResponseMessage> UpdateAsync(HttpRequestMessage request, string path, CancellationToken cancellationToken)
    {
        var id = ReadId(path);
        if (!_events.TryGetValue(id, out var existing))
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        var body = await ReadAsync<UpdateCalendarEventRequest>(request, cancellationToken);
        _events[id] = existing with { Details = ToDto(body!.Details), UpdatedAtUtc = _timeProvider.GetUtcNow() };
        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    private HttpResponseMessage Delete(string path)
    {
        var id = ReadId(path);
        if (!_events.ContainsKey(id))
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        DeleteEvent(id);
        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    private static CalendarEventDetailsDto ToDto(CalendarEventDetailsRequest details)
        => new(
            details.Title, details.Description,
            details.Location is { } location ? new EventLocationDto(location.Address, location.Latitude, location.Longitude) : null,
            details.Color, details.StartUtc, details.EndUtc, details.IsAllDay,
            details.Recurrence is { } recurrence
                ? new RecurrenceDto(
                    recurrence.Frequency, recurrence.IntervalCount, recurrence.UntilUtc, recurrence.OccurrenceCount)
                : null,
            details.Guests, details.ReminderMinutesBeforeStart,
            details.ReminderNotificationChannel,
            // Dropping this made a client that sent no priority look exactly like one that did - the
            // fourth fake in this suite to hide a real bug that way. See FakeNotesServer.
            details.Priority,
            // And the fifth: the phone really was dropping this one on every save, and a fake that
            // dropped it too would have called that correct.
            details.NotifyAtStart);

    private static Guid ReadId(string path) => Guid.Parse(path.Split('/')[^1]);

    private static async Task<T?> ReadAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
        => JsonSerializer.Deserialize<T>(
            await request.Content!.ReadAsStringAsync(cancellationToken),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static HttpResponseMessage Json<T>(T payload, HttpStatusCode statusCode = HttpStatusCode.OK)
        => new(statusCode) { Content = JsonContent.Create(payload) };

    public HttpClient ToHttpClient() => new(this, disposeHandler: false) { BaseAddress = new Uri("https://orbit.example/") };
}
