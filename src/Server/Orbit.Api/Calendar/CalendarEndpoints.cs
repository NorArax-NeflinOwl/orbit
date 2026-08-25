using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Orbit.Contracts.Calendar;
using Orbit.Contracts.Sharing;
using Orbit.Core.Abstractions;
using Orbit.Core.Calendar;
using Orbit.Core.Calendar.AcceptCalendarEventShare;
using Orbit.Core.Calendar.AcquireCalendarEventLock;
using Orbit.Core.Calendar.CreateCalendarEvent;
using Orbit.Core.Calendar.DeleteCalendarEvent;
using Orbit.Core.Calendar.GetCalendarEventById;
using Orbit.Core.Calendar.GetCalendarEvents;
using Orbit.Core.Calendar.GetCalendarEventShareStatus;
using Orbit.Core.Calendar.ReleaseCalendarEventLock;
using Orbit.Core.Calendar.ShareCalendarEvent;
using Orbit.Core.Calendar.UpdateCalendarEvent;
using Orbit.Core.Notifications;

namespace Orbit.Api.Calendar;

public static class CalendarEndpoints
{
    public static void MapCalendarEndpoints(this WebApplication app)
    {
        // Every calendar event belongs to exactly one user (see GetUserId below), so the whole group
        // requires a valid, authenticated caller.
        var calendarEvents = app.MapGroup("/api/calendar-events").RequireAuthorization();

        calendarEvents.MapGet("/", async (ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new GetCalendarEventsQuery(GetUserId(user)), cancellationToken);
            return Results.Ok(result.Select(ToDto));
        });

        calendarEvents.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var calendarEvent = await dispatcher.SendAsync(new GetCalendarEventByIdQuery(GetUserId(user), id), cancellationToken);
            return calendarEvent is null ? Results.NotFound() : Results.Ok(ToDto(calendarEvent));
        });

        calendarEvents.MapPost("/", async (
            CreateCalendarEventRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var id = await dispatcher.SendAsync(
                new CreateCalendarEventCommand(GetUserId(user), ToDomainDetails(request.Details)), cancellationToken);
            return Results.Created($"/api/calendar-events/{id}", id);
        });

        calendarEvents.MapPut("/{id:guid}", async (
            Guid id, UpdateCalendarEventRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var outcome = await dispatcher.SendAsync(
                new UpdateCalendarEventCommand(GetUserId(user), id, ToDomainDetails(request.Details)), cancellationToken);
            return ToApiResult(outcome);
        });

        calendarEvents.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var deleted = await dispatcher.SendAsync(new DeleteCalendarEventCommand(GetUserId(user), id), cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        // Mirrors NoteEndpoints' equivalent lock endpoints - see AcquireCalendarEventLockCommand's comment.
        calendarEvents.MapPost("/{id:guid}/lock", async (Guid id, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var outcome = await dispatcher.SendAsync(new AcquireCalendarEventLockCommand(GetUserId(user), id), cancellationToken);
            return ToApiResult(outcome);
        });

        calendarEvents.MapDelete("/{id:guid}/lock", async (Guid id, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            await dispatcher.SendAsync(new ReleaseCalendarEventLockCommand(GetUserId(user), id), cancellationToken);
            return Results.NoContent();
        });

        // Offers a read-only copy of an owned event to another user - see ShareCalendarEventCommand.
        // The client is responsible for notifying the recipient (a chat message carrying the returned
        // share id), since only the browser holds the key material to encrypt that message.
        calendarEvents.MapPost("/{id:guid}/shares", async (
            Guid id, ShareCalendarEventRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var outcome = await dispatcher.SendAsync(
                new ShareCalendarEventCommand(GetUserId(user), id, request.RecipientUserId, RequestEnum.Parse<ShareAccessLevel>(request.AccessLevel, "accessLevel")),
                cancellationToken);
            return outcome is null ? Results.NotFound() : Results.Ok(new ShareResultDto(outcome.ShareId, outcome.AlreadyShared, outcome.AccessLevelRaised));
        });

        // Resolves a share offered to the caller into a read-only copy in their own calendar - see
        // AcceptCalendarEventShareCommand.
        calendarEvents.MapPost("/shares/{shareId:guid}/accept", async (
            Guid shareId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var accepted = await dispatcher.SendAsync(new AcceptCalendarEventShareCommand(GetUserId(user), shareId), cancellationToken);
            return accepted ? Results.NoContent() : Results.NotFound();
        });

        // Lets Chat.razor show an accurate "Akceptuj" vs. "already accepted" state for an event-share
        // message even after a page reload, instead of only remembering what was clicked this session.
        calendarEvents.MapGet("/shares/{shareId:guid}/status", async (
            Guid shareId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var isAccepted = await dispatcher.SendAsync(new GetCalendarEventShareStatusQuery(GetUserId(user), shareId), cancellationToken);
            return isAccepted is null ? Results.NotFound() : Results.Ok(isAccepted);
        });
    }

    /// <summary>
    /// Reads the authenticated user's id out of the JWT's "sub" claim. Safe to assume it's present and
    /// valid: the group requires authorization, and Orbit.Api only ever issues tokens with this claim
    /// (see TokenService).
    /// </summary>
    private static Guid GetUserId(ClaimsPrincipal user)
    {
        var subject = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("Authenticated request is missing a 'sub' claim.");
        return Guid.Parse(subject);
    }

    private static CalendarEventDetails ToDomainDetails(CalendarEventDetailsRequest request)
        => new(
            request.Title,
            request.Description,
            ToDomainLocation(request.Location),
            request.Color,
            request.StartUtc,
            request.EndUtc,
            request.IsAllDay,
            ToDomainRecurrence(request.Recurrence),
            request.Guests,
            request.ReminderMinutesBeforeStart,
            RequestEnum.Parse<NotificationChannel>(request.CreationNotificationChannel, "creationNotificationChannel"),
            RequestEnum.Parse<NotificationChannel>(request.ReminderNotificationChannel, "reminderNotificationChannel"));

    private static EventLocation? ToDomainLocation(EventLocationRequest? request)
        => request is null ? null : new EventLocation(request.Address, request.Latitude, request.Longitude);

    private static EventRecurrence? ToDomainRecurrence(RecurrenceRequest? request)
        => request is null
            ? null
            : new EventRecurrence(RequestEnum.Parse<RecurrenceFrequency>(request.Frequency, "frequency"), request.IntervalCount, request.UntilUtc);

    private static CalendarEventDto ToDto(CalendarEvent calendarEvent)
    {
        var details = calendarEvent.Details;
        var detailsDto = new CalendarEventDetailsDto(
            details.Title,
            details.Description,
            ToLocationDto(details.Location),
            details.Color,
            details.StartUtc,
            details.EndUtc,
            details.IsAllDay,
            ToRecurrenceDto(details.Recurrence),
            details.Guests,
            details.ReminderMinutesBeforeStart,
            details.CreationNotificationChannel.ToString(),
            details.ReminderNotificationChannel.ToString());

        return new CalendarEventDto(
            calendarEvent.Id, detailsDto, calendarEvent.CreatedAtUtc, calendarEvent.UpdatedAtUtc,
            calendarEvent.IsShared, calendarEvent.SharedByUserName, calendarEvent.AccessLevel.ToString(),
            calendarEvent.IsShared ? calendarEvent.UserId : null);
    }

    private static EventLocationDto? ToLocationDto(EventLocation? location)
        => location is null ? null : new EventLocationDto(location.Address, location.Latitude, location.Longitude);

    private static RecurrenceDto? ToRecurrenceDto(EventRecurrence? recurrence)
        => recurrence is null ? null : new RecurrenceDto(recurrence.Frequency.ToString(), recurrence.IntervalCount, recurrence.UntilUtc);

    /// <summary>Maps an EditOutcome onto the corresponding HTTP response - shared by the update and lock-acquire endpoints above.</summary>
    private static IResult ToApiResult(EditOutcome outcome) => outcome.Kind switch
    {
        EditOutcomeKind.Success => Results.NoContent(),
        EditOutcomeKind.Locked => Results.Json(new LockConflictDto(outcome.LockedByUserName!), statusCode: StatusCodes.Status409Conflict),
        _ => Results.NotFound()
    };
}
