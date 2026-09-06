using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
using Orbit.Core.Chat.ClearConversationHistory;
using Orbit.Core.Chat.Groups.LeaveChatGroup;
using Orbit.Core.Chat.Groups.SetGroupArchived;
using Orbit.Core.Chat.SetConversationArchived;
using Orbit.Core.Chat;
using Orbit.Core.Chat.ApproveConversation;
using Orbit.Core.Chat.EditMessage;
using Orbit.Core.Chat.Groups.EditGroupMessage;
using Orbit.Core.Chat.Groups.GetGroupAnnouncements;
using Orbit.Core.Chat.Groups.GetGroupConversation;
using Orbit.Core.Chat.Groups.GetGroupMessageReceipts;
using Orbit.Core.Chat.Groups.MarkGroupConversationAsRead;
using Orbit.Core.Chat.Groups.SendGroupMessage;
using Orbit.Core.Chat.Groups.ShareGroupHistory;
using Orbit.Core.Chat.Groups.ManageChatGroupMembers;
using Orbit.Core.Chat.Groups.GetChatGroups;
using Orbit.Core.Chat.Groups.CreateChatGroup;
using Orbit.Core.Chat.Groups;
using Orbit.Core.Chat.DeleteMessage;
using Orbit.Core.Chat.GetContacts;
using Orbit.Core.Chat.GetConversation;
using Orbit.Core.Chat.GetConversationAccess;
using Orbit.Core.Chat.GetReadReceipt;
using Orbit.Core.Chat.MarkConversationAsRead;
using Orbit.Core.Chat.SendMessage;
using Orbit.Core.Inventories;
using Orbit.Core.Inventories.FinishRestocking;
using Orbit.Core.Inventories.ReconcileRestockList;
using Orbit.Core.Inventories.RestockListSettingsAccess;
using Orbit.Core.Inventories.ExpiryReminders;
using Orbit.Core.Inventories.GetInventoryItems;
using Orbit.Core.Inventories.AcceptInventoryShare;
using Orbit.Core.Inventories.AcquireInventoryLock;
using Orbit.Core.Inventories.ReleaseInventoryLock;
using Orbit.Core.Inventories.CreateInventory;
using Orbit.Core.Inventories.DeleteInventory;
using Orbit.Core.Inventories.GetInventoryById;
using Orbit.Core.Inventories.GetInventories;
using Orbit.Core.Inventories.GetInventoryShareStatus;
using Orbit.Core.Inventories.ShareInventory;
using Orbit.Core.Inventories.UpdateInventory;
using Orbit.Core.LiveUpdates;
using Orbit.Core.Notes;
using Orbit.Core.Permissions;
using Orbit.Core.Permissions.GetUserPermissions;
using Orbit.Core.Permissions.RedeemPermissionCode;
using Orbit.Core.Notes.AcceptNoteShare;
using Orbit.Core.Notes.AcquireNoteLock;
using Orbit.Core.Notes.CreateNote;
using Orbit.Core.Notes.DeleteNote;
using Orbit.Core.Notes.GetNoteById;
using Orbit.Core.Notes.GetNoteShareStatus;
using Orbit.Core.Notes.GetNotes;
using Orbit.Core.Notes.ReleaseNoteLock;
using Orbit.Core.Notes.SetNotePinned;
using Orbit.Core.Notes.ShareNote;
using Orbit.Core.Notes.UpdateNote;
using Orbit.Core.Notifications;
using Orbit.Core.Notifications.GetNotificationEntries;
using Orbit.Core.Notifications.GetNotificationSettings;
using Orbit.Core.Notifications.GetUnreadNotificationEntries;
using Orbit.Core.Notifications.ClearNotifications;
using Orbit.Core.Notifications.GetChangedNotifications;
using Orbit.Core.Notifications.GetNotificationHistory;
using Orbit.Core.Notifications.MarkNotificationsAtUrlRead;
using Orbit.Core.Sharing;
using Orbit.Core.Sharing.ClaimPublicShareLink;
using Orbit.Core.Sharing.CreatePublicShareLink;
using Orbit.Core.Sharing.GetPublicSharedItem;
using Orbit.Core.Sharing.RevokePublicShareLink;
using Orbit.Core.Transfer;
using Orbit.Core.Transfer.ExportArchive;
using Orbit.Core.Transfer.ImportArchive;
using Orbit.Core.Notifications.MarkAllNotificationsRead;
using Orbit.Core.Notifications.UpdateNotificationSettings;
using Orbit.Core.PushNotifications.SubscribeDeviceToPush;
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
using Orbit.Core.Tasks.LinkCalendarEventToTaskList;
using Orbit.Core.Tasks.MoveTaskItem;
using Orbit.Core.Tasks.OverdueNotifications;
using Orbit.Core.Tasks.ReleaseTaskListLock;
using Orbit.Core.Tasks.LinkTaskListToInventory;
using Orbit.Core.Tasks.GenerateInventoryFromTaskList;
using Orbit.Core.Tasks.GetTaskListStockCheck;
using Orbit.Core.Tasks.StockCheck;
using Orbit.Core.Tasks.RaiseStockShortfalls;
using Orbit.Core.Tasks.SetTaskListPinned;
using Orbit.Core.Tasks.ShareTaskList;
using Orbit.Core.Tasks.GetInventoryReferences;
using Orbit.Core.Tasks.UpdateTaskList;
using Orbit.Core.Suggestions;
using Orbit.Core.Suggestions.GetNameSuggestions;
using Orbit.Core.Suggestions.GetUsedValues;
using Orbit.Core.Users;
using Orbit.Core.Users.SetPresence;
using Orbit.Core.Users.SetPrivacyChoice;
using Orbit.Core.Users.SaveOwnLocation;
using Orbit.Core.Location.GetSharedLocations;
using Orbit.Core.Location.StopReceivingLocation;
using Orbit.Core.Location.StopSharingLocation;
using Orbit.Core.Location.ShareLocation;
using Orbit.Core.Location;
using Orbit.Core.Users.GetUserById;
using Orbit.Core.Users.GetUsersByIds;
using Orbit.Core.Users.GetWrappedPrivateKey;
using Orbit.Core.Users.Login;
using Orbit.Core.Users.RegisterUser;
using Orbit.Core.Users.ChangePassword;
using Orbit.Core.Users.DeleteAccount;
using Orbit.Core.Users.ConfirmEmailVerification;
using Orbit.Core.Users.RequestEmailVerification;
using Orbit.Core.Users.RequestPasswordReset;
using Orbit.Core.Users.ResetPassword;
using Orbit.Core.Users.UpdateProfile;
using Orbit.Core.Users.SignInWithGoogle;
using Orbit.Core.Users.LinkGoogleAccount;
using Orbit.Core.Users.SetPassword;
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
        // Depends on the task, inventory and share repositories (all scoped), so it must be scoped too -
        // shared by the three paths that hand a task list to somebody: an offer by name, accepting one,
        // and claiming a public link.
        services.AddScoped<TaskListShareCascade>();
        services.AddScoped<IRequestHandler<CreateTaskListCommand, Guid>, CreateTaskListCommandHandler>();
        services.AddScoped<IRequestHandler<UpdateTaskListCommand, EditOutcome>, UpdateTaskListCommandHandler>();
        services.AddScoped<IRequestHandler<MoveTaskItemCommand, EditOutcome>, MoveTaskItemCommandHandler>();
        services.AddScoped<IRequestHandler<LinkCalendarEventToTaskListCommand, EditOutcome>, LinkCalendarEventToTaskListCommandHandler>();
        services.AddScoped<IRequestHandler<DeleteTaskListCommand, bool>, DeleteTaskListCommandHandler>();
        services.AddScoped<IRequestHandler<GetTaskListsQuery, IReadOnlyList<TaskList>>, GetTaskListsQueryHandler>();
        services.AddScoped<IRequestHandler<GetTaskListByIdQuery, TaskList?>, GetTaskListByIdQueryHandler>();
        services.AddScoped<IRequestHandler<ShareTaskListCommand, ShareOutcome?>, ShareTaskListCommandHandler>();
        services.AddScoped<IRequestHandler<SetTaskListPinnedCommand, bool>, SetTaskListPinnedCommandHandler>();
        services.AddScoped<IRequestHandler<LinkTaskListToInventoryCommand, bool>, LinkTaskListToInventoryCommandHandler>();
        services.AddScoped<IRequestHandler<GetTaskListStockCheckQuery, TaskListStockCheck?>, GetTaskListStockCheckQueryHandler>();
        services.AddScoped<IRequestHandler<RaiseStockShortfallsCommand, int>, RaiseStockShortfallsCommandHandler>();
        services.AddScoped<IRequestHandler<GenerateInventoryFromTaskListCommand, Guid?>, GenerateInventoryFromTaskListCommandHandler>();
        services.AddScoped<IRequestHandler<SetNotePinnedCommand, bool>, SetNotePinnedCommandHandler>();
        services.AddScoped<IRequestHandler<SetAvailabilityCommand, bool>, SetAvailabilityCommandHandler>();
        // The footer's "Do not share my personal information" - see User.KeepsThirdPartiesOut.
        services.AddScoped<IRequestHandler<SetPrivacyChoiceCommand, bool>, SetPrivacyChoiceCommandHandler>();
        services.AddScoped<IRequestHandler<PresenceHeartbeatCommand, bool>, PresenceHeartbeatCommandHandler>();
        services.AddScoped<UserVisibility>();
        services.AddScoped<PermissionCodeStore>();
        services.AddScoped<UserVisibility>();
        services.AddScoped<IRequestHandler<GetUserPermissionsQuery, IReadOnlyList<ApplicationPermission>>, GetUserPermissionsQueryHandler>();
        services.AddScoped<IRequestHandler<RedeemPermissionCodeCommand, RedeemPermissionCodeOutcome>, RedeemPermissionCodeCommandHandler>();
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
        services.AddScoped<IRequestHandler<UpdateProfileCommand, UpdateProfileResult>, UpdateProfileCommandHandler>();
        services.AddScoped<IRequestHandler<ChangePasswordCommand, bool>, ChangePasswordCommandHandler>();
        services.AddScoped<IRequestHandler<SetPasswordCommand, bool>, SetPasswordCommandHandler>();
        services.AddScoped<IRequestHandler<SignInWithGoogleCommand, User?>, SignInWithGoogleCommandHandler>();
        services.AddScoped<IRequestHandler<LinkGoogleAccountCommand, LinkGoogleAccountResult>, LinkGoogleAccountCommandHandler>();
        services.AddScoped<IRequestHandler<UnlinkGoogleAccountCommand, LinkGoogleAccountResult>, UnlinkGoogleAccountCommandHandler>();
        services.AddScoped<IRequestHandler<DeleteAccountCommand, bool>, DeleteAccountCommandHandler>();
        services.AddScoped<IRequestHandler<RequestEmailVerificationCommand, EmailVerificationRequestResult>, RequestEmailVerificationCommandHandler>();
        services.AddScoped<IRequestHandler<ConfirmEmailVerificationCommand, EmailVerificationConfirmResult>, ConfirmEmailVerificationCommandHandler>();
        services.AddScoped<IRequestHandler<RequestPasswordResetCommand, bool>, RequestPasswordResetCommandHandler>();
        services.AddScoped<IRequestHandler<ResetPasswordCommand, bool>, ResetPasswordCommandHandler>();
        services.AddScoped<IRequestHandler<LoginQuery, LoginResult>, LoginQueryHandler>();
        services.AddScoped<IRequestHandler<SearchUserQuery, User?>, SearchUserQueryHandler>();
        services.AddScoped<IRequestHandler<GetUserByIdQuery, User?>, GetUserByIdQueryHandler>();
        services.AddScoped<IRequestHandler<GetUsersByIdsQuery, IReadOnlyList<User>>, GetUsersByIdsQueryHandler>();
        services.AddScoped<IRequestHandler<SetPublicKeyCommand, bool>, SetPublicKeyCommandHandler>();
        services.AddScoped<IRequestHandler<SetEncryptionKeyCommand, bool>, SetEncryptionKeyCommandHandler>();
        services.AddScoped<IRequestHandler<GetWrappedPrivateKeyQuery, WrappedPrivateKey?>, GetWrappedPrivateKeyQueryHandler>();

        services.AddScoped<IRequestHandler<SendMessageCommand, SendMessageResult>, SendMessageCommandHandler>();
        services.AddScoped<IRequestHandler<EditMessageCommand, EditMessageResult>, EditMessageCommandHandler>();
        services.AddScoped<IRequestHandler<DeleteChatMessageCommand, bool>, DeleteChatMessageCommandHandler>();
        services.AddScoped<IRequestHandler<SaveOwnLocationCommand, bool>, SaveOwnLocationCommandHandler>();

        // Sharing a position with one contact, encrypted for them - see SharedLocation.
        services.AddScoped<IRequestHandler<ShareLocationCommand, bool>, ShareLocationCommandHandler>();
        services.AddScoped<IRequestHandler<StopSharingLocationCommand, bool>, StopSharingLocationCommandHandler>();
        services.AddScoped<IRequestHandler<StopReceivingLocationCommand, bool>, StopReceivingLocationCommandHandler>();
        services.AddScoped<IRequestHandler<GetSharedLocationsQuery, IReadOnlyList<SharedLocation>>, GetSharedLocationsQueryHandler>();
        services.AddScoped<IRequestHandler<GetOwnLocationSharesQuery, IReadOnlyList<SharedLocation>>, GetOwnLocationSharesQueryHandler>();

        // Group chat: the group itself, its membership, and the fan-out that keeps group messages
        // encrypted under the same pairwise keys one-to-one chat uses.
        services.AddScoped<IRequestHandler<CreateChatGroupCommand, Guid>, CreateChatGroupCommandHandler>();
        services.AddScoped<IRequestHandler<GetChatGroupsQuery, IReadOnlyList<ChatGroup>>, GetChatGroupsQueryHandler>();
        services.AddScoped<IRequestHandler<AddChatGroupMemberCommand, bool>, AddChatGroupMemberCommandHandler>();
        services.AddScoped<IRequestHandler<RemoveChatGroupMemberCommand, bool>, RemoveChatGroupMemberCommandHandler>();
        services.AddScoped<IRequestHandler<ChangeChatGroupMemberRoleCommand, bool>, ChangeChatGroupMemberRoleCommandHandler>();
        services.AddScoped<IRequestHandler<SendGroupMessageCommand, bool>, SendGroupMessageCommandHandler>();
        services.AddScoped<IRequestHandler<GetGroupConversationQuery, IReadOnlyList<GroupConversationEntry>>, GetGroupConversationQueryHandler>();
        services.AddScoped<IRequestHandler<EditGroupMessageCommand, bool>, EditGroupMessageCommandHandler>();
        services.AddScoped<IRequestHandler<MarkGroupConversationAsReadCommand, bool>, MarkGroupConversationAsReadCommandHandler>();
        services.AddScoped<IRequestHandler<GetGroupMessageReceiptsQuery, IReadOnlyList<GroupMessageReceipt>>, GetGroupMessageReceiptsQueryHandler>();
        // What a newcomer is given of the conversation they arrived late to, and the line that says so.
        services.AddScoped<IRequestHandler<ShareGroupHistoryCommand, int>, ShareGroupHistoryCommandHandler>();
        services.AddScoped<IRequestHandler<GetGroupAnnouncementsQuery, IReadOnlyList<ChatGroupAnnouncement>>, GetGroupAnnouncementsQueryHandler>();
        services.AddScoped<IRequestHandler<GetConversationQuery, IReadOnlyList<ChatMessage>>, GetConversationQueryHandler>();
        services.AddScoped<IRequestHandler<GetContactsQuery, IReadOnlyList<ContactSummary>>, GetContactsQueryHandler>();
        services.AddScoped<IRequestHandler<MarkConversationAsReadCommand, bool>, MarkConversationAsReadCommandHandler>();
        services.AddScoped<IRequestHandler<GetReadReceiptQuery, DateTimeOffset?>, GetReadReceiptQueryHandler>();
        services.AddScoped<IRequestHandler<GetConversationAccessQuery, ChatConversationAccess?>, GetConversationAccessQueryHandler>();
        services.AddScoped<IRequestHandler<ApproveConversationCommand, bool>, ApproveConversationCommandHandler>();

        services.AddScoped<IRequestHandler<SubscribeToPushCommand, bool>, SubscribeToPushCommandHandler>();
        services.AddScoped<IRequestHandler<SubscribeDeviceToPushCommand, bool>, SubscribeDeviceToPushCommandHandler>();
        services.AddScoped<IRequestHandler<UnsubscribeFromPushCommand, bool>, UnsubscribeFromPushCommandHandler>();
        // Depends on IPushSubscriptionRepository (scoped, backed by the DbContext), so it must be scoped
        // too - called directly (not through IDispatcher) by SendMessageCommandHandler above and, in
        // Orbit.Api, by CalendarEventReminderBackgroundService and OverdueTaskNotificationBackgroundService.
        services.AddScoped<PushNotificationDispatcher>();

        services.AddScoped<IRequestHandler<GetNotificationSettingsQuery, NotificationSettings>, GetNotificationSettingsQueryHandler>();
        services.AddScoped<IRequestHandler<UpdateNotificationSettingsCommand, NotificationSettings>, UpdateNotificationSettingsCommandHandler>();
        services.AddScoped<IRequestHandler<GetNotificationEntriesQuery, IReadOnlyList<NotificationEntry>>, GetNotificationEntriesQueryHandler>();
        services.AddScoped<IRequestHandler<GetUnreadNotificationEntriesQuery, IReadOnlyList<NotificationEntry>>, GetUnreadNotificationEntriesQueryHandler>();
        services.AddScoped<IRequestHandler<ClearNotificationsCommand, bool>, ClearNotificationsCommandHandler>();
        services.AddScoped<IRequestHandler<MarkAllNotificationsReadCommand, bool>, MarkAllNotificationsReadCommandHandler>();
        services.AddScoped<IRequestHandler<MarkNotificationsAtUrlReadCommand, bool>, MarkNotificationsAtUrlReadCommandHandler>();
        services.AddScoped<IRequestHandler<GetNotificationHistoryQuery, IReadOnlyList<NotificationEntry>>, GetNotificationHistoryQueryHandler>();
        services.AddScoped<IRequestHandler<GetChangedNotificationsQuery, IReadOnlyList<NotificationEntry>>, GetChangedNotificationsQueryHandler>();

        services.AddScoped<PublicSharedItemReader>();
        services.AddScoped<IRequestHandler<CreatePublicShareLinkCommand, PublicShareLink?>, CreatePublicShareLinkCommandHandler>();
        services.AddScoped<IRequestHandler<RevokePublicShareLinkCommand, bool>, RevokePublicShareLinkCommandHandler>();
        services.AddScoped<IRequestHandler<GetPublicSharedItemQuery, PublicSharedItem?>, GetPublicSharedItemQueryHandler>();
        services.AddScoped<IRequestHandler<ClaimPublicShareLinkCommand, ClaimPublicShareLinkResult>, ClaimPublicShareLinkCommandHandler>();

        services.AddScoped<IRequestHandler<ExportArchiveQuery, OrbitArchive>, ExportArchiveQueryHandler>();
        services.AddScoped<IRequestHandler<ImportArchiveCommand, ImportArchiveResult>, ImportArchiveCommandHandler>();
        // Depends on the two repositories above (scoped, backed by the DbContext), so it must be scoped
        // too - called directly (not through IDispatcher) by SendMessageCommandHandler and, in Orbit.Api,
        // by each of the four reminder background services, the same way they already call
        // PushNotificationDispatcher directly.
        services.AddScoped<NotificationRecorder>();
        services.AddScoped<ISharedItemNotifier, SharedItemNotifier>();

        // Depends on ITaskRepository (scoped, backed by the DbContext), so it must be scoped too.
        services.AddScoped<PendingRestockTaskResolver>();
        services.AddScoped<InventoryTaskListCoordinator>();
        // Writing an inventory's item list, shared by the command that creates one already holding
        // rows and the command that saves one - see InventoryItemsSaver.
        services.AddScoped<InventoryItemsSaver>();
        services.AddScoped<RestockCompletion>();
        services.AddScoped<StockedEntryCompletion>();
        services.AddScoped<RestockListRefresh>();

        // How an inventory's restock list is built and when it comes round, plus the manual rebuild.
        services.AddScoped<IRequestHandler<GetRestockListSettingsQuery, RestockListSettings?>, GetRestockListSettingsQueryHandler>();
        services.AddScoped<IRequestHandler<SaveRestockListSettingsCommand, RestockRefreshOutcome>, SaveRestockListSettingsCommandHandler>();
        services.AddScoped<IRequestHandler<RefreshRestockListCommand, RestockRefreshOutcome>, RefreshRestockListCommandHandler>();
        services.AddScoped<IRequestHandler<ReconcileRestockListCommand, RestockOutcome>, ReconcileRestockListCommandHandler>();
        services.AddScoped<IRequestHandler<GetInventoryReferencesQuery, IReadOnlyList<InventoryReference>>, GetInventoryReferencesQueryHandler>();

        // Names the reader has already used, offered as they type one - see GetNameSuggestionsQuery for
        // why this is a database question rather than a question for the assistant.
        services.AddScoped<IRequestHandler<GetNameSuggestionsQuery, IReadOnlyList<NameSuggestion>>, GetNameSuggestionsQueryHandler>();
        // The whole of the reader's own vocabulary for one field, rather than what resembles what
        // they are typing - see UsedValueKind.
        services.AddScoped<IRequestHandler<GetUsedValuesQuery, IReadOnlyList<string>>, GetUsedValuesQueryHandler>();
        services.AddScoped<IRequestHandler<FinishRestockingCommand, int>, FinishRestockingCommandHandler>();
        services.AddScoped<IRequestHandler<GetInventoryItemsQuery, IReadOnlyList<InventoryItem>?>, GetInventoryItemsQueryHandler>();

        // Inventories - the container inventory items now belong to, with Notes-style sharing on top.
        services.AddScoped<InventoryAccessResolver>();
        services.AddScoped<IRequestHandler<CreateInventoryCommand, Guid>, CreateInventoryCommandHandler>();
        services.AddScoped<IRequestHandler<GetInventoriesQuery, IReadOnlyList<Inventory>>, GetInventoriesQueryHandler>();
        services.AddScoped<IRequestHandler<GetInventoryByIdQuery, Inventory?>, GetInventoryByIdQueryHandler>();
        services.AddScoped<IRequestHandler<UpdateInventoryCommand, EditOutcome>, UpdateInventoryCommandHandler>();
        services.AddScoped<IRequestHandler<DeleteInventoryCommand, bool>, DeleteInventoryCommandHandler>();
        services.AddScoped<IRequestHandler<ShareInventoryCommand, ShareOutcome?>, ShareInventoryCommandHandler>();
        services.AddScoped<IRequestHandler<AcceptInventoryShareCommand, bool>, AcceptInventoryShareCommandHandler>();
        services.AddScoped<IRequestHandler<GetInventoryShareStatusQuery, bool?>, GetInventoryShareStatusQueryHandler>();
        services.AddScoped<IRequestHandler<AcquireInventoryLockCommand, EditOutcome>, AcquireInventoryLockCommandHandler>();
        services.AddScoped<IRequestHandler<ReleaseInventoryLockCommand, bool>, ReleaseInventoryLockCommandHandler>();
        // Depends on IInventoryExpiryNotificationRepository (scoped, backed by the DbContext), so it
        // must be scoped too - used by Orbit.Api's InventoryExpiryReminderBackgroundService, not
        // through IDispatcher, for the same reason as OverdueTaskNotificationScheduler above.
        services.AddScoped<InventoryExpiryReminderScheduler>();

        // Announcing a change is unconditional at every call site, so something always has to be here.
        // A host that has a live connection to announce over replaces this - see Orbit.Api's
        // AddOrbitLiveUpdates - and TryAdd rather than Add is what lets it, by registering first.
        services.TryAddSingleton<ILiveUpdatePublisher, SilentLiveUpdatePublisher>();

        services.AddScoped<IRequestHandler<SetConversationArchivedCommand, bool>, SetConversationArchivedCommandHandler>();
        services.AddScoped<IRequestHandler<SetGroupArchivedCommand, bool>, SetGroupArchivedCommandHandler>();
        services.AddScoped<IRequestHandler<ClearConversationHistoryCommand, bool>, ClearConversationHistoryCommandHandler>();
        services.AddScoped<IRequestHandler<LeaveChatGroupCommand, bool>, LeaveChatGroupCommandHandler>();

        services.AddScoped<Dispatcher>();
        services.AddScoped<IDispatcher>(provider => new LoggingDispatcher(
            provider.GetRequiredService<Dispatcher>(),
            provider.GetRequiredService<ILogger<LoggingDispatcher>>()));

        return services;
    }
}
