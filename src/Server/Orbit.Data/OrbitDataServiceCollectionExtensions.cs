using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orbit.Core.Calendar;
using Orbit.Core.Calendar.Reminders;
using Orbit.Core.Chat;
using Orbit.Core.Notes;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.DailyReminders;
using Orbit.Core.Tasks.OverdueNotifications;
using Orbit.Core.Users;
using Orbit.Data.Repositories;

namespace Orbit.Data;

public static class OrbitDataServiceCollectionExtensions
{
    /// <summary>
    /// Registers the PostgreSQL-backed persistence layer and its repositories. The provider lives only
    /// here - swapping it again only touches this method.
    /// </summary>
    public static IServiceCollection AddOrbitData(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Orbit")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Orbit is not configured. Set the ORBIT_DB_CONNECTION_STRING environment " +
                "variable (see .env.example) when running via Docker Compose, or run " +
                "`dotnet user-secrets set \"ConnectionStrings:Orbit\" \"<connection string>\"` for local `dotnet run`.");

        services.AddDbContext<OrbitDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<INoteRepository, NoteRepository>();
        services.AddScoped<INoteShareRepository, NoteShareRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<ITaskListShareRepository, TaskListShareRepository>();
        services.AddScoped<ICalendarEventRepository, CalendarEventRepository>();
        services.AddScoped<ICalendarEventShareRepository, CalendarEventShareRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IEventReminderRepository, EventReminderRepository>();
        services.AddScoped<IContactRepository, ContactRepository>();
        services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
        services.AddScoped<IChatConversationAccessRepository, ChatConversationAccessRepository>();
        services.AddScoped<IPushSubscriptionRepository, PushSubscriptionRepository>();
        services.AddScoped<IOverdueTaskNotificationRepository, OverdueTaskNotificationRepository>();
        services.AddScoped<IDailyTaskReminderRepository, DailyTaskReminderRepository>();

        return services;
    }
}
