using Microsoft.Extensions.Logging;
using Orbit.Core.Abstractions;
using Orbit.Core.Notifications;
using Orbit.Core.Users;

namespace Orbit.Core.Calendar.CreateCalendarEvent;

public sealed class CreateCalendarEventCommandHandler : IRequestHandler<CreateCalendarEventCommand, Guid>
{
    private readonly ICalendarEventRepository _calendarEventRepository;
    private readonly IUserRepository _userRepository;
    private readonly IEmailSender _emailSender;
    private readonly PushNotificationDispatcher _pushNotificationDispatcher;
    private readonly ILogger<CreateCalendarEventCommandHandler> _logger;

    public CreateCalendarEventCommandHandler(
        ICalendarEventRepository calendarEventRepository,
        IUserRepository userRepository,
        IEmailSender emailSender,
        PushNotificationDispatcher pushNotificationDispatcher,
        ILogger<CreateCalendarEventCommandHandler> logger)
    {
        _calendarEventRepository = calendarEventRepository;
        _userRepository = userRepository;
        _emailSender = emailSender;
        _pushNotificationDispatcher = pushNotificationDispatcher;
        _logger = logger;
    }

    public async Task<Guid> HandleAsync(CreateCalendarEventCommand request, CancellationToken cancellationToken)
    {
        var calendarEvent = CalendarEvent.Create(request.UserId, request.Details);
        await _calendarEventRepository.AddAsync(calendarEvent, cancellationToken);

        if (calendarEvent.Details.CreationNotificationChannel != NotificationChannel.None)
        {
            await SendCreationNotificationAsync(calendarEvent, cancellationToken);
        }

        return calendarEvent.Id;
    }

    /// <summary>
    /// Best-effort: the event is already persisted by the time this runs, so a transient failure to
    /// notify must not turn an otherwise successful creation into a failed request - it's only logged
    /// (push failures are already best-effort inside PushNotificationDispatcher itself).
    /// </summary>
    private async Task SendCreationNotificationAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken)
    {
        var channel = calendarEvent.Details.CreationNotificationChannel;

        if (channel.HasFlag(NotificationChannel.Push))
        {
            var payload = EventCreationPushContent.Build(calendarEvent.Details, calendarEvent.Id);
            await _pushNotificationDispatcher.NotifyUserAsync(calendarEvent.UserId, payload, cancellationToken);
        }

        if (!channel.HasFlag(NotificationChannel.Email))
        {
            return;
        }

        try
        {
            var owner = await _userRepository.GetByIdAsync(calendarEvent.UserId, cancellationToken);
            if (owner is null)
            {
                return;
            }

            var (subject, body) = EventCreationEmailContent.Build(calendarEvent.Details);
            await _emailSender.SendAsync(owner.Email, subject, body, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception, "Failed to send the event-created notification for calendar event {CalendarEventId}", calendarEvent.Id);
        }
    }
}
