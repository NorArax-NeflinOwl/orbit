using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orbit.Core.Calendar;
using Orbit.Core.Diagnostics;
using Orbit.Core.Folders;
using Orbit.Core.Calendar.Reminders;
using Orbit.Core.Chat;
using Orbit.Core.Suggestions;
using Orbit.Core.Chat.Groups;
using Orbit.Core.Location;
using Orbit.Core.Inventories;
using Orbit.Core.Inventories.ExpiryReminders;
using Orbit.Core.Notes;
using Orbit.Core.Places;
using Orbit.Core.Permissions;
using Orbit.Core.Notifications;
using Orbit.Core.Sharing;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.DailyReminders;
using Orbit.Core.Tasks.OverdueNotifications;
using Orbit.Core.Sync;
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
        // Which ConnectionStrings:* entry to read - "Orbit" unless the launch profile says otherwise.
        // The "(Azure DB)" launch profiles set Database__ConnectionStringName=OrbitAzure, so one
        // machine keeps both the local and the Azure connection in user secrets and F5 picks between
        // them; the value itself never appears in a committed file.
        var connectionStringName = configuration["Database:ConnectionStringName"] ?? "Orbit";
        var connectionString = configuration.GetConnectionString(connectionStringName)
            ?? throw new InvalidOperationException(
                $"ConnectionStrings:{connectionStringName} is not configured. docker-compose.yml sets " +
                "ConnectionStrings:Orbit from POSTGRES_PASSWORD (see .env.example) when running via " +
                "Docker Compose. For local `dotnet run`, run `dotnet user-secrets set " +
                $"\"ConnectionStrings:{connectionStringName}\" \"Host=<host>;Port=5432;Database=orbit;" +
                "Username=orbit;Password=<password>\"` in src/Server/Orbit.Api (localhost with the " +
                "POSTGRES_PASSWORD from your .env for the local database; the Azure server's FQDN and " +
                "password for OrbitAzure).");

        services.AddDbContext<OrbitDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IUserPermissionRepository, UserPermissionRepository>();
        services.AddScoped<IPermissionCodeRepository, PermissionCodeRepository>();
        services.AddScoped<INoteRepository, NoteRepository>();
        services.AddScoped<IPlaceRepository, PlaceRepository>();
        services.AddScoped<IPlaceShareRepository, PlaceShareRepository>();
        services.AddScoped<IFolderRepository, FolderRepository>();
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
        services.AddScoped<IChatGroupRepository, ChatGroupRepository>();
        services.AddScoped<IChatGroupAnnouncementRepository, ChatGroupAnnouncementRepository>();
        services.AddScoped<INameSuggestionRepository, NameSuggestionRepository>();
        services.AddScoped<IUsedValueRepository, UsedValueRepository>();
        services.AddScoped<ISharedLocationRepository, SharedLocationRepository>();
        services.AddScoped<IChatConversationAccessRepository, ChatConversationAccessRepository>();
        services.AddScoped<IPushSubscriptionRepository, PushSubscriptionRepository>();
        services.AddScoped<IOverdueTaskNotificationRepository, OverdueTaskNotificationRepository>();
        services.AddScoped<IDailyTaskReminderRepository, DailyTaskReminderRepository>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IInventoryShareRepository, InventoryShareRepository>();
        services.AddScoped<IInventoryItemRepository, InventoryItemRepository>();
        services.AddScoped<IInventoryManagedTaskListRepository, InventoryManagedTaskListRepository>();
        services.AddScoped<IInventoryExpiryNotificationRepository, InventoryExpiryNotificationRepository>();
        services.AddScoped<INotificationSettingsRepository, NotificationSettingsRepository>();
        services.AddScoped<Orbit.Core.Tags.ITagColourRepository, TagColourRepository>();
        services.AddScoped<INotificationEntryRepository, NotificationEntryRepository>();
        services.AddScoped<IDiagnosticLogRepository, DiagnosticLogRepository>();
        services.AddScoped<ISyncTombstoneRepository, SyncTombstoneRepository>();
        services.AddScoped<IPublicShareLinkRepository, PublicShareLinkRepository>();

        return services;
    }
}
