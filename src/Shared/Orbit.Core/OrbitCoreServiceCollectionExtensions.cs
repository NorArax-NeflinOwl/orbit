using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orbit.Core.Abstractions;
using Orbit.Core.Calendar;
using Orbit.Core.Calendar.AcceptCalendarEventShare;
using Orbit.Core.Calendar.CreateCalendarEvent;
using Orbit.Core.Calendar.DeleteCalendarEvent;
using Orbit.Core.Calendar.GetCalendarEventById;
using Orbit.Core.Calendar.GetCalendarEvents;
using Orbit.Core.Calendar.GetCalendarEventShareStatus;
using Orbit.Core.Calendar.Reminders;
using Orbit.Core.Calendar.ShareCalendarEvent;
using Orbit.Core.Calendar.UpdateCalendarEvent;
using Orbit.Core.Chat;
using Orbit.Core.Chat.ApproveConversation;
using Orbit.Core.Chat.GetContacts;
using Orbit.Core.Chat.GetConversation;
using Orbit.Core.Chat.GetConversationAccess;
using Orbit.Core.Chat.GetReadReceipt;
using Orbit.Core.Chat.MarkConversationAsRead;
using Orbit.Core.Chat.SendMessage;
using Orbit.Core.Notes;
using Orbit.Core.Notes.CreateNote;
using Orbit.Core.Notes.DeleteNote;
using Orbit.Core.Notes.GetNoteById;
using Orbit.Core.Notes.GetNotes;
using Orbit.Core.Notes.UpdateNote;
using Orbit.Core.Notifications;
using Orbit.Core.PushNotifications.SubscribeToPush;
using Orbit.Core.PushNotifications.UnsubscribeFromPush;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.CreateTaskList;
using Orbit.Core.Tasks.DeleteTaskList;
using Orbit.Core.Tasks.GetTaskListById;
using Orbit.Core.Tasks.GetTaskLists;
using Orbit.Core.Tasks.OverdueNotifications;
using Orbit.Core.Tasks.UpdateTaskList;
using Orbit.Core.Users;
using Orbit.Core.Users.GetUserById;
using Orbit.Core.Users.Login;
using Orbit.Core.Users.RegisterUser;
using Orbit.Core.Users.SearchUser;
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
        services.AddScoped<IRequestHandler<CreateNoteCommand, Guid>, CreateNoteCommandHandler>();
        services.AddScoped<IRequestHandler<UpdateNoteCommand, bool>, UpdateNoteCommandHandler>();
        services.AddScoped<IRequestHandler<DeleteNoteCommand, bool>, DeleteNoteCommandHandler>();
        services.AddScoped<IRequestHandler<GetNotesQuery, IReadOnlyList<Note>>, GetNotesQueryHandler>();
        services.AddScoped<IRequestHandler<GetNoteByIdQuery, Note?>, GetNoteByIdQueryHandler>();

        services.AddScoped<IRequestHandler<CreateTaskListCommand, Guid>, CreateTaskListCommandHandler>();
        services.AddScoped<IRequestHandler<UpdateTaskListCommand, bool>, UpdateTaskListCommandHandler>();
        services.AddScoped<IRequestHandler<DeleteTaskListCommand, bool>, DeleteTaskListCommandHandler>();
        services.AddScoped<IRequestHandler<GetTaskListsQuery, IReadOnlyList<TaskList>>, GetTaskListsQueryHandler>();
        services.AddScoped<IRequestHandler<GetTaskListByIdQuery, TaskList?>, GetTaskListByIdQueryHandler>();
        // Depends on ITaskRepository (scoped, backed by the DbContext), so it must be scoped too.
        services.AddScoped<TaskListLinkValidator>();
        // Stateless per call - safe to share a single instance for the app's lifetime.
        services.AddSingleton<LinkedTaskCompletionResolver>();
        // Depends on IOverdueTaskNotificationRepository (scoped, backed by the DbContext), so it must be
        // scoped too - used by Orbit.Api's OverdueTaskNotificationBackgroundService, not through
        // IDispatcher, since it's a system-level poll rather than a per-user command or query (mirrors
        // EventReminderScheduler below).
        services.AddScoped<OverdueTaskNotificationScheduler>();

        services.AddScoped<IRequestHandler<CreateCalendarEventCommand, Guid>, CreateCalendarEventCommandHandler>();
        services.AddScoped<IRequestHandler<UpdateCalendarEventCommand, bool>, UpdateCalendarEventCommandHandler>();
        services.AddScoped<IRequestHandler<DeleteCalendarEventCommand, bool>, DeleteCalendarEventCommandHandler>();
        services.AddScoped<IRequestHandler<GetCalendarEventsQuery, IReadOnlyList<CalendarEvent>>, GetCalendarEventsQueryHandler>();
        services.AddScoped<IRequestHandler<GetCalendarEventByIdQuery, CalendarEvent?>, GetCalendarEventByIdQueryHandler>();
        services.AddScoped<IRequestHandler<ShareCalendarEventCommand, Guid?>, ShareCalendarEventCommandHandler>();
        services.AddScoped<IRequestHandler<AcceptCalendarEventShareCommand, bool>, AcceptCalendarEventShareCommandHandler>();
        services.AddScoped<IRequestHandler<GetCalendarEventShareStatusQuery, bool?>, GetCalendarEventShareStatusQueryHandler>();
        // Depends on IEventReminderRepository (scoped, backed by the DbContext), so it must be scoped
        // too - used by Orbit.Api's CalendarEventReminderBackgroundService, not through IDispatcher,
        // since it's a system-level poll rather than a per-user command or query.
        services.AddScoped<EventReminderScheduler>();

        services.AddScoped<IRequestHandler<RegisterUserCommand, RegisterUserResult>, RegisterUserCommandHandler>();
        services.AddScoped<IRequestHandler<LoginQuery, User?>, LoginQueryHandler>();
        services.AddScoped<IRequestHandler<SearchUserQuery, User?>, SearchUserQueryHandler>();
        services.AddScoped<IRequestHandler<GetUserByIdQuery, User?>, GetUserByIdQueryHandler>();
        services.AddScoped<IRequestHandler<SetPublicKeyCommand, bool>, SetPublicKeyCommandHandler>();

        services.AddScoped<IRequestHandler<SendMessageCommand, SendMessageResult>, SendMessageCommandHandler>();
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

        services.AddScoped<Dispatcher>();
        services.AddScoped<IDispatcher>(provider => new LoggingDispatcher(
            provider.GetRequiredService<Dispatcher>(),
            provider.GetRequiredService<ILogger<LoggingDispatcher>>()));

        return services;
    }
}
