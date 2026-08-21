using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Calendar;
using Orbit.Core.Calendar.CreateCalendarEvent;
using Orbit.Core.Notifications;
using Orbit.Core.Users;
using Xunit;

namespace Orbit.Api.Tests.Calendar;

public sealed class CreateCalendarEventCommandHandlerTests
{
    private static readonly CalendarEventDetails DefaultDetails = new(
        "Title", null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), false, null, [], [],
        CreationNotificationChannel: NotificationChannel.None, ReminderNotificationChannel: NotificationChannel.None);

    [Fact]
    public async Task HandleAsync_creates_a_calendar_event_owned_by_the_requesting_user()
    {
        var repository = new InMemoryCalendarEventRepository();
        var handler = CreateHandler(repository);
        var userId = Guid.NewGuid();
        var details = DefaultDetails with { Title = "Team sync" };

        var eventId = await handler.HandleAsync(new CreateCalendarEventCommand(userId, details), CancellationToken.None);

        var stored = await repository.GetByIdAsync(userId, eventId, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal("Team sync", stored!.Details.Title);
    }

    [Fact]
    public async Task HandleAsync_throws_when_the_event_ends_before_it_starts()
    {
        var handler = CreateHandler(new InMemoryCalendarEventRepository());
        var now = DateTimeOffset.UtcNow;
        var details = DefaultDetails with { StartUtc = now, EndUtc = now.AddHours(-1) };

        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(new CreateCalendarEventCommand(Guid.NewGuid(), details), CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_creates_a_calendar_event_with_a_map_location()
    {
        var repository = new InMemoryCalendarEventRepository();
        var handler = CreateHandler(repository);
        var userId = Guid.NewGuid();
        var location = new EventLocation("Rynek Główny 1, Kraków", 50.0617, 19.9373);
        var details = DefaultDetails with { Location = location };

        var eventId = await handler.HandleAsync(new CreateCalendarEventCommand(userId, details), CancellationToken.None);

        var stored = await repository.GetByIdAsync(userId, eventId, CancellationToken.None);
        Assert.Equal(location, stored!.Details.Location);
    }

    [Theory]
    [InlineData(90.1, 19.9373)]
    [InlineData(-90.1, 19.9373)]
    [InlineData(50.0617, 180.1)]
    [InlineData(50.0617, -180.1)]
    public async Task HandleAsync_throws_when_the_locations_coordinates_are_out_of_range(double latitude, double longitude)
    {
        var handler = CreateHandler(new InMemoryCalendarEventRepository());
        var details = DefaultDetails with { Location = new EventLocation(null, latitude, longitude) };

        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(new CreateCalendarEventCommand(Guid.NewGuid(), details), CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_emails_the_owner_when_notify_on_creation_is_enabled()
    {
        var userRepository = new InMemoryUserRepository();
        var owner = User.FromPersistence(Guid.NewGuid(), "owner@example.com", "owner", "Owner", "hash", DateTimeOffset.UtcNow, null);
        await userRepository.AddAsync(owner, CancellationToken.None);
        var emailSender = new RecordingEmailSender();
        var handler = CreateHandler(new InMemoryCalendarEventRepository(), userRepository, emailSender);
        var details = DefaultDetails with { Title = "Team sync", CreationNotificationChannel = NotificationChannel.Email };

        await handler.HandleAsync(new CreateCalendarEventCommand(owner.Id, details), CancellationToken.None);

        var sentEmail = Assert.Single(emailSender.SentEmails);
        Assert.Equal(owner.Email, sentEmail.ToEmailAddress);
        Assert.Contains("Team sync", sentEmail.Subject);
    }

    [Fact]
    public async Task HandleAsync_does_not_email_the_owner_when_notify_on_creation_is_disabled()
    {
        var userRepository = new InMemoryUserRepository();
        var owner = User.FromPersistence(Guid.NewGuid(), "owner@example.com", "owner", "Owner", "hash", DateTimeOffset.UtcNow, null);
        await userRepository.AddAsync(owner, CancellationToken.None);
        var emailSender = new RecordingEmailSender();
        var handler = CreateHandler(new InMemoryCalendarEventRepository(), userRepository, emailSender);
        var details = DefaultDetails with { CreationNotificationChannel = NotificationChannel.None };

        await handler.HandleAsync(new CreateCalendarEventCommand(owner.Id, details), CancellationToken.None);

        Assert.Empty(emailSender.SentEmails);
    }

    [Fact]
    public async Task HandleAsync_still_creates_the_event_when_sending_the_creation_email_fails()
    {
        var repository = new InMemoryCalendarEventRepository();
        var userRepository = new InMemoryUserRepository();
        var owner = User.FromPersistence(Guid.NewGuid(), "owner@example.com", "owner", "Owner", "hash", DateTimeOffset.UtcNow, null);
        await userRepository.AddAsync(owner, CancellationToken.None);
        var handler = CreateHandler(repository, userRepository, new ThrowingEmailSender());
        var details = DefaultDetails with { Title = "Team sync", CreationNotificationChannel = NotificationChannel.Email };

        var eventId = await handler.HandleAsync(new CreateCalendarEventCommand(owner.Id, details), CancellationToken.None);

        var stored = await repository.GetByIdAsync(owner.Id, eventId, CancellationToken.None);
        Assert.NotNull(stored);
    }

    private static CreateCalendarEventCommandHandler CreateHandler(
        InMemoryCalendarEventRepository repository, InMemoryUserRepository? userRepository = null, IEmailSender? emailSender = null)
        => new(
            repository,
            userRepository ?? new InMemoryUserRepository(),
            emailSender ?? new RecordingEmailSender(),
            new PushNotificationDispatcher(
                new InMemoryPushSubscriptionRepository(), new RecordingPushNotificationSender(),
                NullLogger<PushNotificationDispatcher>.Instance),
            NullLogger<CreateCalendarEventCommandHandler>.Instance);

    /// <summary>Simulates a transient SMTP failure to verify creation stays successful despite it.</summary>
    private sealed class ThrowingEmailSender : IEmailSender
    {
        public Task SendAsync(string toEmailAddress, string subject, string body, CancellationToken cancellationToken)
            => throw new InvalidOperationException("SMTP server unreachable.");
    }
}
