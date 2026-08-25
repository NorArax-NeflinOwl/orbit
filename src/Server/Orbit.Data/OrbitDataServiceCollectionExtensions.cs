using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orbit.Core.Calendar;
using Orbit.Core.Calendar.Reminders;
using Orbit.Core.Chat;
using Orbit.Core.Inventory;
using Orbit.Core.Inventory.ExpiryReminders;
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
                "ConnectionStrings:Orbit is not configured. docker-compose.yml sets this from " +
                "POSTGRES_PASSWORD (see .env.example) when running via Docker Compose. For local " +
                "`dotnet run` against that same container, run `dotnet user-secrets set " +
                "\"ConnectionStrings:Orbit\" \"Host=localhost;Port=5432;Database=orbit;Username=orbit;" +
                "Password=<the POSTGRES_PASSWORD from your .env>\"`.");

        services.AddDbContext<OrbitDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<INoteRepository, NoteRepository>();
        services.AddScoped<INoteShareRepository, NoteShareRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<ITaskListShareRepository, TaskListShareRepository>();
        services.AddScoped<ICalendarEventRepository, CalendarEventRepository>();
        services.AddScoped<ICalendarEventShareRepository, CalendarEventShareRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAccountDeletionRepository, AccountDeletionRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IUserVerificationCodeRepository, UserVerificationCodeRepository>();
        services.AddScoped<IEventReminderRepository, EventReminderRepository>();
        services.AddScoped<IContactRepository, ContactRepository>();
        services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
        services.AddScoped<IChatConversationAccessRepository, ChatConversationAccessRepository>();
        services.AddScoped<IPushSubscriptionRepository, PushSubscriptionRepository>();
        services.AddScoped<IOverdueTaskNotificationRepository, OverdueTaskNotificationRepository>();
        services.AddScoped<IDailyTaskReminderRepository, DailyTaskReminderRepository>();
        services.AddScoped<IWarehouseRepository, WarehouseRepository>();
        services.AddScoped<IWarehouseShareRepository, WarehouseShareRepository>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IInventoryManagedTaskListRepository, InventoryManagedTaskListRepository>();
        services.AddScoped<IInventoryExpiryNotificationRepository, InventoryExpiryNotificationRepository>();
        services.AddScoped<INotificationSettingsRepository, NotificationSettingsRepository>();
        services.AddScoped<INotificationEntryRepository, NotificationEntryRepository>();

        return services;
    }
}
