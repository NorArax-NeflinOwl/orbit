using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
using Orbit.Core.Calendar.Reminders;
using Orbit.Core.Calendar.ShareCalendarEvent;
using Orbit.Core.Calendar.UpdateCalendarEvent;
using Orbit.Core.Chat;
using Orbit.Core.Chat.ApproveConversation;
using Orbit.Core.Chat.EditMessage;
using Orbit.Core.Chat.GetContacts;
using Orbit.Core.Chat.GetConversation;
using Orbit.Core.Chat.GetConversationAccess;
using Orbit.Core.Chat.GetReadReceipt;
using Orbit.Core.Chat.MarkConversationAsRead;
using Orbit.Core.Chat.SendMessage;
using Orbit.Core.Inventory;
using Orbit.Core.Inventory.CreateInventoryItem;
using Orbit.Core.Inventory.DeleteInventoryItem;
using Orbit.Core.Inventory.ExpiryReminders;
using Orbit.Core.Inventory.GetInventoryItemById;
using Orbit.Core.Inventory.GetInventoryItems;
using Orbit.Core.Inventory.UpdateInventoryItem;
using Orbit.Core.Notes;
using Orbit.Core.Notes.AcceptNoteShare;
using Orbit.Core.Notes.AcquireNoteLock;
using Orbit.Core.Notes.CreateNote;
using Orbit.Core.Notes.DeleteNote;
using Orbit.Core.Notes.GetNoteById;
using Orbit.Core.Notes.GetNoteShareStatus;
using Orbit.Core.Notes.GetNotes;
using Orbit.Core.Notes.ReleaseNoteLock;
using Orbit.Core.Notes.ShareNote;
using Orbit.Core.Notes.UpdateNote;
using Orbit.Core.Notifications;
using Orbit.Core.PushNotifications.SubscribeToPush;
using Orbit.Core.PushNotifications.UnsubscribeFromPush;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.AcceptTaskListShare;
using Orbit.Core.Tasks.AcquireTaskListLock;
using Orbit.Core.Tasks.CreateTaskList;
using Orbit.Core.Tasks.DailyReminders;
using Orbit.Core.Tasks.DeleteTaskList;
using Orbit.Core.Tasks.GetTaskListById;
using Orbit.Core.Tasks.GetTaskListShareStatus;
using Orbit.Core.Tasks.GetTaskLists;
using Orbit.Core.Tasks.MoveTaskItem;
using Orbit.Core.Tasks.OverdueNotifications;
using Orbit.Core.Tasks.ReleaseTaskListLock;
using Orbit.Core.Tasks.ShareTaskList;
using Orbit.Core.Tasks.UpdateTaskList;
using Orbit.Core.Users;
using Orbit.Core.Users.GetUserById;
using Orbit.Core.Users.GetWrappedPrivateKey;
using Orbit.Core.Users.Login;
using Orbit.Core.Users.RegisterUser;
using Orbit.Core.Users.SearchUser;
using Orbit.Core.Users.SetEncryptionKey;
using Orbit.Core.Users.SetPublicKey;

namespace Orbit.Core;

public static class OrbitCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers all request handlers and wraps <see cref="IDispatcher"/> with logging/timing
    /// (see <see cref="LoggingDispatcher"/>), so every command and query is traced the same way.
    /// </summary>
    public static IServiceCollection AddOrbitCore(this IServiceCollection services)
    {
        // Depends on INoteRepository/INoteShareRepository/IUserRepository (all scoped, backed by the
        // DbContext), so it must be scoped too - shared by every Notes handler below that needs to know
        // how the calling user relates to a note (owner vs. shared-with, and at what access level).
        services.AddScoped<NoteAccessResolver>();
        services.AddScoped<IRequestHandler<CreateNoteCommand, Guid>, CreateNoteCommandHandler>();
        services.AddScoped<IRequestHandler<UpdateNoteCommand, EditOutcome>, UpdateNoteCommandHandler>();
        services.AddScoped<IRequestHandler<DeleteNoteCommand, bool>, DeleteNoteCommandHandler>();
        services.AddScoped<IRequestHandler<GetNotesQuery, IReadOnlyList<Note>>, GetNotesQueryHandler>();
        services.AddScoped<IRequestHandler<GetNoteByIdQuery, Note?>, GetNoteByIdQueryHandler>();
        services.AddScoped<IRequestHandler<ShareNoteCommand, ShareOutcome?>, ShareNoteCommandHandler>();
        services.AddScoped<IRequestHandler<AcceptNoteShareCommand, bool>, AcceptNoteShareCommandHandler>();
        services.AddScoped<IRequestHandler<GetNoteShareStatusQuery, bool?>, GetNoteShareStatusQueryHandler>();
        services.AddScoped<IRequestHandler<AcquireNoteLockCommand, EditOutcome>, AcquireNoteLockCommandHandler>();
        services.AddScoped<IRequestHandler<ReleaseNoteLockCommand, bool>, ReleaseNoteLockCommandHandler>();

        // Depends on ITaskRepository/ITaskListShareRepository/IUserRepository (all scoped), so it must
        // be scoped too - mirrors NoteAccessResolver's registration above.
        services.AddScoped<TaskListAccessResolver>();
        services.AddScoped<IRequestHandler<CreateTaskListCommand, Guid>, CreateTaskListCommandHandler>();
        services.AddScoped<IRequestHandler<UpdateTaskListCommand, EditOutcome>, UpdateTaskListCommandHandler>();
        services.AddScoped<IRequestHandler<MoveTaskItemCommand, EditOutcome>, MoveTaskItemCommandHandler>();
        services.AddScoped<IRequestHandler<DeleteTaskListCommand, bool>, DeleteTaskListCommandHandler>();
        services.AddScoped<IRequestHandler<GetTaskListsQuery, IReadOnlyList<TaskList>>, GetTaskListsQueryHandler>();
        services.AddScoped<IRequestHandler<GetTaskListByIdQuery, TaskList?>, GetTaskListByIdQueryHandler>();
        services.AddScoped<IRequestHandler<ShareTaskListCommand, ShareOutcome?>, ShareTaskListCommandHandler>();
        services.AddScoped<IRequestHandler<AcceptTaskListShareCommand, bool>, AcceptTaskListShareCommandHandler>();
        services.AddScoped<IRequestHandler<GetTaskListShareStatusQuery, bool?>, GetTaskListShareStatusQueryHandler>();
        services.AddScoped<IRequestHandler<AcquireTaskListLockCommand, EditOutcome>, AcquireTaskListLockCommandHandler>();
        services.AddScoped<IRequestHandler<ReleaseTaskListLockCommand, bool>, ReleaseTaskListLockCommandHandler>();
        // Depends on ITaskRepository (scoped, backed by the DbContext), so it must be scoped too.
        services.AddScoped<TaskListLinkValidator>();
        // Stateless per call - safe to share a single instance for the app's lifetime.
        services.AddSingleton<LinkedTaskCompletionResolver>();
        // Depends on IOverdueTaskNotificationRepository (scoped, backed by the DbContext), so it must be
        // scoped too - used by Orbit.Api's OverdueTaskNotificationBackgroundService, not through
        // IDispatcher, since it's a system-level poll rather than a per-user command or query (mirrors
        // EventReminderScheduler below).
        services.AddScoped<OverdueTaskNotificationScheduler>();
        // Depends on IDailyTaskReminderRepository (scoped, backed by the DbContext), so it must be scoped
        // too - used by Orbit.Api's DailyTaskReminderBackgroundService, not through IDispatcher, for the
        // same reason as OverdueTaskNotificationScheduler above.
        services.AddScoped<DailyTaskReminderScheduler>();

        // Depends on ICalendarEventRepository/ICalendarEventShareRepository/IUserRepository (all
        // scoped), so it must be scoped too - mirrors NoteAccessResolver's registration above.
        services.AddScoped<CalendarEventAccessResolver>();
        services.AddScoped<IRequestHandler<CreateCalendarEventCommand, Guid>, CreateCalendarEventCommandHandler>();
        services.AddScoped<IRequestHandler<UpdateCalendarEventCommand, EditOutcome>, UpdateCalendarEventCommandHandler>();
        services.AddScoped<IRequestHandler<DeleteCalendarEventCommand, bool>, DeleteCalendarEventCommandHandler>();
        services.AddScoped<IRequestHandler<GetCalendarEventsQuery, IReadOnlyList<CalendarEvent>>, GetCalendarEventsQueryHandler>();
        services.AddScoped<IRequestHandler<GetCalendarEventByIdQuery, CalendarEvent?>, GetCalendarEventByIdQueryHandler>();
        services.AddScoped<IRequestHandler<ShareCalendarEventCommand, ShareOutcome?>, ShareCalendarEventCommandHandler>();
        services.AddScoped<IRequestHandler<AcceptCalendarEventShareCommand, bool>, AcceptCalendarEventShareCommandHandler>();
        services.AddScoped<IRequestHandler<GetCalendarEventShareStatusQuery, bool?>, GetCalendarEventShareStatusQueryHandler>();
        services.AddScoped<IRequestHandler<AcquireCalendarEventLockCommand, EditOutcome>, AcquireCalendarEventLockCommandHandler>();
        services.AddScoped<IRequestHandler<ReleaseCalendarEventLockCommand, bool>, ReleaseCalendarEventLockCommandHandler>();
        // Depends on IEventReminderRepository (scoped, backed by the DbContext), so it must be scoped
        // too - used by Orbit.Api's CalendarEventReminderBackgroundService, not through IDispatcher,
        // since it's a system-level poll rather than a per-user command or query.
        services.AddScoped<EventReminderScheduler>();

        services.AddScoped<IRequestHandler<RegisterUserCommand, RegisterUserResult>, RegisterUserCommandHandler>();
        services.AddScoped<IRequestHandler<LoginQuery, User?>, LoginQueryHandler>();
        services.AddScoped<IRequestHandler<SearchUserQuery, User?>, SearchUserQueryHandler>();
        services.AddScoped<IRequestHandler<GetUserByIdQuery, User?>, GetUserByIdQueryHandler>();
        services.AddScoped<IRequestHandler<SetPublicKeyCommand, bool>, SetPublicKeyCommandHandler>();
        services.AddScoped<IRequestHandler<SetEncryptionKeyCommand, bool>, SetEncryptionKeyCommandHandler>();
        services.AddScoped<IRequestHandler<GetWrappedPrivateKeyQuery, WrappedPrivateKey?>, GetWrappedPrivateKeyQueryHandler>();

        services.AddScoped<IRequestHandler<SendMessageCommand, SendMessageResult>, SendMessageCommandHandler>();
        services.AddScoped<IRequestHandler<EditMessageCommand, EditMessageResult>, EditMessageCommandHandler>();
        services.AddScoped<IRequestHandler<GetConversationQuery, IReadOnlyList<ChatMessage>>, GetConversationQueryHandler>();
        services.AddScoped<IRequestHandler<GetContactsQuery, IReadOnlyList<ContactSummary>>, GetContactsQueryHandler>();
        services.AddScoped<IRequestHandler<MarkConversationAsReadCommand, bool>, MarkConversationAsReadCommandHandler>();
        services.AddScoped<IRequestHandler<GetReadReceiptQuery, DateTimeOffset?>, GetReadReceiptQueryHandler>();
        services.AddScoped<IRequestHandler<GetConversationAccessQuery, ChatConversationAccess?>, GetConversationAccessQueryHandler>();
        services.AddScoped<IRequestHandler<ApproveConversationCommand, bool>, ApproveConversationCommandHandler>();

        services.AddScoped<IRequestHandler<SubscribeToPushCommand, bool>, SubscribeToPushCommandHandler>();
        services.AddScoped<IRequestHandler<UnsubscribeFromPushCommand, bool>, UnsubscribeFromPushCommandHandler>();
        // Depends on IPushSubscriptionRepository (scoped, backed by the DbContext), so it must be scoped
        // too - called directly (not through IDispatcher) by SendMessageCommandHandler above and, in
        // Orbit.Api, by CalendarEventReminderBackgroundService and OverdueTaskNotificationBackgroundService.
        services.AddScoped<PushNotificationDispatcher>();

        // Depends on ITaskRepository (scoped, backed by the DbContext), so it must be scoped too.
        services.AddScoped<PendingRestockTaskResolver>();
        services.AddScoped<InventoryTaskListCoordinator>();
        services.AddScoped<IRequestHandler<CreateInventoryItemCommand, Guid>, CreateInventoryItemCommandHandler>();
        services.AddScoped<IRequestHandler<UpdateInventoryItemCommand, EditOutcome>, UpdateInventoryItemCommandHandler>();
        services.AddScoped<IRequestHandler<DeleteInventoryItemCommand, bool>, DeleteInventoryItemCommandHandler>();
        services.AddScoped<IRequestHandler<GetInventoryItemsQuery, IReadOnlyList<InventoryItem>>, GetInventoryItemsQueryHandler>();
        services.AddScoped<IRequestHandler<GetInventoryItemByIdQuery, InventoryItem?>, GetInventoryItemByIdQueryHandler>();
        // Depends on IInventoryExpiryNotificationRepository (scoped, backed by the DbContext), so it
        // must be scoped too - used by Orbit.Api's InventoryExpiryReminderBackgroundService, not
        // through IDispatcher, for the same reason as OverdueTaskNotificationScheduler above.
        services.AddScoped<InventoryExpiryReminderScheduler>();

        services.AddScoped<Dispatcher>();
        services.AddScoped<IDispatcher>(provider => new LoggingDispatcher(
            provider.GetRequiredService<Dispatcher>(),
            provider.GetRequiredService<ILogger<LoggingDispatcher>>()));

        return services;
    }
}
