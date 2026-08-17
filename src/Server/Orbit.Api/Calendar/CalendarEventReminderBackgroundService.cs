using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orbit.Api.HealthChecks;
using Orbit.Core.Calendar.Reminders;
using Orbit.Core.Notifications;
using Orbit.Core.Users;

namespace Orbit.Api.Calendar;

/// <summary>
/// Periodically checks for calendar event reminders that have come due and emails the event's owner
/// about each one exactly once. Lives entirely in Orbit.Api: sending a real email needs an SMTP
/// connection and credentials that must never reach the Blazor WebAssembly client (see
/// GeocodingApiClient's class comment in Orbit.Web for the same reasoning applied to a different
/// third-party call).
/// </summary>
public sealed class CalendarEventReminderBackgroundService : BackgroundService
{
    private const string ServiceName = "CalendarEventReminders";
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan LookBackWindow = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly HostedServiceHealthTracker _healthTracker;
    private readonly ILogger<CalendarEventReminderBackgroundService> _logger;

    public CalendarEventReminderBackgroundService(
        IServiceScopeFactory serviceScopeFactory,
        HostedServiceHealthTracker healthTracker,
        ILogger<CalendarEventReminderBackgroundService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _healthTracker = healthTracker;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        do
        {
            try
            {
                await SendDueRemindersAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A single failed poll (e.g. the SMTP server is temporarily unreachable) must not stop
                // this background service - the next tick tries again.
                _logger.LogError(exception, "Failed to send calendar event reminder emails");
            }

            // Reported even after a failed poll: the loop itself is still alive and will try again,
            // which is exactly what HostedServicesHealthCheck needs to know.
            _healthTracker.ReportHeartbeat(ServiceName);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SendDueRemindersAsync(CancellationToken cancellationToken)
    {
        // A fresh DI scope per poll: EventReminderScheduler and its repositories are scoped services
        // (backed by OrbitDbContext), while this background service itself is a singleton.
        using var scope = _serviceScopeFactory.CreateScope();
        var scheduler = scope.ServiceProvider.GetRequiredService<EventReminderScheduler>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var eventReminderRepository = scope.ServiceProvider.GetRequiredService<IEventReminderRepository>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        var dueReminders = await scheduler.FindDueRemindersAsync(DateTimeOffset.UtcNow, LookBackWindow, cancellationToken);
        foreach (var dueReminder in dueReminders)
        {
            await SendReminderEmailAsync(dueReminder, userRepository, eventReminderRepository, emailSender, cancellationToken);
        }
    }

    private static async Task SendReminderEmailAsync(
        DueEventReminder dueReminder,
        IUserRepository userRepository,
        IEventReminderRepository eventReminderRepository,
        IEmailSender emailSender,
        CancellationToken cancellationToken)
    {
        var calendarEvent = dueReminder.CalendarEvent;
        var owner = await userRepository.GetByIdAsync(calendarEvent.UserId, cancellationToken);
        if (owner is null)
        {
            // The owning account was deleted after the event was created - nothing meaningful to notify.
            return;
        }

        var (subject, body) = EventReminderEmailContent.Build(calendarEvent.Details, dueReminder.MinutesBeforeStart);
        await emailSender.SendAsync(owner.Email, subject, body, cancellationToken);
        await eventReminderRepository.MarkAsSentAsync(calendarEvent.Id, dueReminder.MinutesBeforeStart, DateTimeOffset.UtcNow, cancellationToken);
    }
}
