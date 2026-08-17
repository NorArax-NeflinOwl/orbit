using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orbit.Core.Abstractions;
using Orbit.Core.Calendar;
using Orbit.Core.Calendar.CreateCalendarEvent;
using Orbit.Core.Calendar.GetCalendarEventById;
using Orbit.Core.Calendar.GetCalendarEvents;
using Orbit.Core.Calendar.Reminders;
using Orbit.Core.Calendar.UpdateCalendarEvent;
using Orbit.Core.Notes;
using Orbit.Core.Notes.CreateNote;
using Orbit.Core.Notes.GetNoteById;
using Orbit.Core.Notes.GetNotes;
using Orbit.Core.Notes.UpdateNote;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.CreateTaskList;
using Orbit.Core.Tasks.GetTaskListById;
using Orbit.Core.Tasks.GetTaskLists;
using Orbit.Core.Tasks.UpdateTaskList;
using Orbit.Core.Users;
using Orbit.Core.Users.Login;
using Orbit.Core.Users.RegisterUser;

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
        services.AddScoped<IRequestHandler<GetNotesQuery, IReadOnlyList<Note>>, GetNotesQueryHandler>();
        services.AddScoped<IRequestHandler<GetNoteByIdQuery, Note?>, GetNoteByIdQueryHandler>();

        services.AddScoped<IRequestHandler<CreateTaskListCommand, Guid>, CreateTaskListCommandHandler>();
        services.AddScoped<IRequestHandler<UpdateTaskListCommand, bool>, UpdateTaskListCommandHandler>();
        services.AddScoped<IRequestHandler<GetTaskListsQuery, IReadOnlyList<TaskList>>, GetTaskListsQueryHandler>();
        services.AddScoped<IRequestHandler<GetTaskListByIdQuery, TaskList?>, GetTaskListByIdQueryHandler>();
        // Depends on ITaskRepository (scoped, backed by the DbContext), so it must be scoped too.
        services.AddScoped<TaskListLinkValidator>();
        // Stateless per call - safe to share a single instance for the app's lifetime.
        services.AddSingleton<LinkedTaskCompletionResolver>();

        services.AddScoped<IRequestHandler<CreateCalendarEventCommand, Guid>, CreateCalendarEventCommandHandler>();
        services.AddScoped<IRequestHandler<UpdateCalendarEventCommand, bool>, UpdateCalendarEventCommandHandler>();
        services.AddScoped<IRequestHandler<GetCalendarEventsQuery, IReadOnlyList<CalendarEvent>>, GetCalendarEventsQueryHandler>();
        services.AddScoped<IRequestHandler<GetCalendarEventByIdQuery, CalendarEvent?>, GetCalendarEventByIdQueryHandler>();
        // Depends on IEventReminderRepository (scoped, backed by the DbContext), so it must be scoped
        // too - used by Orbit.Api's CalendarEventReminderBackgroundService, not through IDispatcher,
        // since it's a system-level poll rather than a per-user command or query.
        services.AddScoped<EventReminderScheduler>();

        services.AddScoped<IRequestHandler<RegisterUserCommand, RegisterUserResult>, RegisterUserCommandHandler>();
        services.AddScoped<IRequestHandler<LoginQuery, User?>, LoginQueryHandler>();

        services.AddScoped<Dispatcher>();
        services.AddScoped<IDispatcher>(provider => new LoggingDispatcher(
            provider.GetRequiredService<Dispatcher>(),
            provider.GetRequiredService<ILogger<LoggingDispatcher>>()));

        return services;
    }
}
