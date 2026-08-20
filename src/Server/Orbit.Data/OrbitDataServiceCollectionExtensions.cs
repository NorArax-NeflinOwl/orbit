using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orbit.Core.Calendar;
using Orbit.Core.Calendar.Reminders;
using Orbit.Core.Chat;
using Orbit.Core.Notes;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.OverdueNotifications;
using Orbit.Core.Users;
using Orbit.Data.Repositories;

namespace Orbit.Data;

public static class OrbitDataServiceCollectionExtensions
{
    /// <summary>
    /// Registers the SQLite-backed persistence layer and its repositories. SQLite is a prototype
    /// choice for zero-setup local development; swapping to another provider only touches this method.
    /// </summary>
    public static IServiceCollection AddOrbitData(this IServiceCollection services, IConfiguration configuration)
    {
        // SQLite locks the whole database file for the duration of a write, and its own default busy
        // timeout is 0 - so without this, two writes landing at the same time (e.g. a chat message
        // being saved while a background service or another request also writes) fail immediately
        // with "database is locked" instead of one simply waiting for the other. Five seconds is enough
        // slack for that to resolve itself under normal load without letting a genuinely stuck request
        // hang.
        var connectionStringBuilder = new SqliteConnectionStringBuilder(
            configuration.GetConnectionString("Orbit") ?? "Data Source=orbit.db")
        {
            DefaultTimeout = 5
        };

        services.AddDbContext<OrbitDbContext>(options => options.UseSqlite(connectionStringBuilder.ToString()));
        services.AddScoped<INoteRepository, NoteRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
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

        return services;
    }
}
