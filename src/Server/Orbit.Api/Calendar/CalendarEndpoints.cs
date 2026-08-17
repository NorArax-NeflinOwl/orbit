using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Orbit.Contracts.Calendar;
using Orbit.Core.Abstractions;
using Orbit.Core.Calendar;
using Orbit.Core.Calendar.AcceptCalendarEventShare;
using Orbit.Core.Calendar.CreateCalendarEvent;
using Orbit.Core.Calendar.DeleteCalendarEvent;
using Orbit.Core.Calendar.GetCalendarEventById;
using Orbit.Core.Calendar.GetCalendarEvents;
using Orbit.Core.Calendar.GetCalendarEventShareStatus;
using Orbit.Core.Calendar.ShareCalendarEvent;
using Orbit.Core.Calendar.UpdateCalendarEvent;

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
            var updated = await dispatcher.SendAsync(
                new UpdateCalendarEventCommand(GetUserId(user), id, ToDomainDetails(request.Details)), cancellationToken);
            return updated ? Results.NoContent() : Results.NotFound();
        });

        calendarEvents.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var deleted = await dispatcher.SendAsync(new DeleteCalendarEventCommand(GetUserId(user), id), cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        // Offers a read-only copy of an owned event to another user - see ShareCalendarEventCommand.
        // The client is responsible for notifying the recipient (a chat message carrying the returned
        // share id), since only the browser holds the key material to encrypt that message.
        calendarEvents.MapPost("/{id:guid}/shares", async (
            Guid id, ShareCalendarEventRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var shareId = await dispatcher.SendAsync(
                new ShareCalendarEventCommand(GetUserId(user), id, request.RecipientUserId), cancellationToken);
            return shareId is null ? Results.NotFound() : Results.Ok(shareId);
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
            request.NotifyOnCreation,
            request.NotifyBeforeStart);

    private static EventLocation? ToDomainLocation(EventLocationRequest? request)
        => request is null ? null : new EventLocation(request.Address, request.Latitude, request.Longitude);

    private static EventRecurrence? ToDomainRecurrence(RecurrenceRequest? request)
        => request is null
            ? null
            : new EventRecurrence(Enum.Parse<RecurrenceFrequency>(request.Frequency, ignoreCase: true), request.IntervalCount, request.UntilUtc);

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
            details.NotifyOnCreation,
            details.NotifyBeforeStart);

        return new CalendarEventDto(
            calendarEvent.Id, detailsDto, calendarEvent.CreatedAtUtc, calendarEvent.UpdatedAtUtc,
            calendarEvent.IsShared, calendarEvent.SharedByUserName);
    }

    private static EventLocationDto? ToLocationDto(EventLocation? location)
        => location is null ? null : new EventLocationDto(location.Address, location.Latitude, location.Longitude);

    private static RecurrenceDto? ToRecurrenceDto(EventRecurrence? recurrence)
        => recurrence is null ? null : new RecurrenceDto(recurrence.Frequency.ToString(), recurrence.IntervalCount, recurrence.UntilUtc);
}
