using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Mobile.Api;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Data;
using Orbit.Mobile.Notifications;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.Crypto;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Notifications;

/// <summary>
/// Following a notification to the thing it is about. The parsing is pinned down next door; this is
/// about the step after it, where a user id has to become a conversation the reader can actually open.
/// </summary>
public sealed class NotificationOpeningTests
{
    [Fact]
    public async Task A_message_notification_opens_the_conversation_with_whoever_sent_it()
    {
        using var context = new OpeningContext();
        var senderUserId = await context.AddKnownContactAsync("Bob");

        var outcome = await context.Opener.OpenAsync($"/chat/{senderUserId}");

        Assert.Equal(NotificationOpenOutcome.Opened, outcome);
        Assert.Equal("ShowConversation", context.Navigator.LastDestination);
        Assert.Equal(senderUserId, context.Navigator.LastContact!.UserId);
    }

    [Fact]
    public async Task A_first_message_from_somebody_new_still_opens_the_conversation()
    {
        // The case the whole retry exists for. A notification arrives the moment the message is sent,
        // which is before any sync has pulled the sender into this phone's contact list - so the
        // obvious implementation lands on "not found" exactly when the feature matters most.
        using var context = new OpeningContext();
        var senderUserId = context.AddContactOnTheServerOnly("Someone new");

        var outcome = await context.Opener.OpenAsync($"/chat/{senderUserId}");

        Assert.Equal(NotificationOpenOutcome.Opened, outcome);
        Assert.Equal(senderUserId, context.Navigator.LastContact!.UserId);
    }

    [Fact]
    public async Task Somebody_who_is_not_a_contact_at_all_still_opens()
    {
        // A shared note notifies with the same /chat/{userId} path as a message, but sharing does not
        // make anybody a contact - the server only counts one once a conversation has happened. Found
        // on a device: refreshing contacts here searches forever for somebody who will never be in the
        // list, so the reader is told the app cannot find what it plainly just told them about.
        using var context = new OpeningContext();
        var sharerUserId = context.AddStranger("Sharer");

        var outcome = await context.Opener.OpenAsync($"/chat/{sharerUserId}");

        Assert.Equal(NotificationOpenOutcome.Opened, outcome);
        Assert.Equal(sharerUserId, context.Navigator.LastContact!.UserId);
        Assert.Equal("Sharer", context.Navigator.LastContact.DisplayName);
    }

    [Fact]
    public async Task Somebody_the_phone_cannot_look_up_says_so_rather_than_doing_nothing()
    {
        using var context = new OpeningContext();
        context.GoOffline();

        var outcome = await context.Opener.OpenAsync($"/chat/{Guid.NewGuid()}");

        // A tap that does nothing at all reads as the app being broken; this is the caller's cue to say
        // what happened.
        Assert.Equal(NotificationOpenOutcome.NotOnThisPhoneYet, outcome);
        Assert.Empty(context.Navigator.Destinations);
    }

    [Fact]
    public async Task A_group_invitation_opens_the_group_it_names()
    {
        using var context = new OpeningContext();
        var groupId = await context.AddKnownGroupAsync("Walking club");

        var outcome = await context.Opener.OpenAsync($"/chat/groups/{groupId}");

        Assert.Equal(NotificationOpenOutcome.Opened, outcome);
        Assert.Equal("ShowGroupConversation", context.Navigator.LastDestination);
        Assert.Equal(groupId, context.Navigator.LastGroup!.Id);
    }

    [Fact]
    public async Task A_group_the_phone_has_not_pulled_yet_is_fetched_rather_than_refused()
    {
        using var context = new OpeningContext();
        var groupId = context.AddGroupOnTheServerOnly("Just added me");

        var outcome = await context.Opener.OpenAsync($"/chat/groups/{groupId}");

        Assert.Equal(NotificationOpenOutcome.Opened, outcome);
        Assert.Equal(groupId, context.Navigator.LastGroup!.Id);
    }

    /// <summary>
    /// The list, by the id the screen is opened with. The path names it by its server id and every
    /// screen on the phone takes the local one - handing the navigator what the path said opened a
    /// detail screen for a list that does not exist here, with no title, no entries and nothing saying
    /// why. Nothing failed and nothing was logged; it just looked like an empty list.
    /// </summary>
    [Fact]
    public async Task A_task_reminder_opens_the_list_it_is_about()
    {
        using var context = new OpeningContext();
        var taskList = await context.AddKnownTaskListAsync("Shopping");

        var outcome = await context.Opener.OpenAsync($"/tasks/{taskList.ServerId}");

        Assert.Equal(NotificationOpenOutcome.Opened, outcome);
        Assert.Equal(taskList.LocalId, context.Navigator.LastTaskListId);
    }

    /// <summary>
    /// The same race as a first message: sharing a list notifies the other person straight away, which
    /// is before their phone has pulled anything down.
    /// </summary>
    [Fact]
    public async Task A_list_shared_a_moment_ago_is_pulled_rather_than_refused()
    {
        using var context = new OpeningContext();
        var serverId = context.AddTaskListOnTheServerOnly("Weekend jobs");

        var outcome = await context.Opener.OpenAsync($"/tasks/{serverId}");

        Assert.Equal(NotificationOpenOutcome.Opened, outcome);
        Assert.NotNull(context.Navigator.LastTaskListId);
        Assert.NotEqual(serverId, context.Navigator.LastTaskListId);
    }

    [Fact]
    public async Task A_list_the_phone_cannot_fetch_says_so_rather_than_opening_an_empty_screen()
    {
        using var context = new OpeningContext();
        context.GoOffline();

        var outcome = await context.Opener.OpenAsync($"/tasks/{Guid.NewGuid()}");

        Assert.Equal(NotificationOpenOutcome.NotOnThisPhoneYet, outcome);
        Assert.Empty(context.Navigator.Destinations);
    }

    [Theory]
    [InlineData("/calendar/00000000-0000-0000-0000-000000000001", "ShowCalendar")]
    [InlineData("/inventory", "ShowInventory")]
    [InlineData("/map", "ShowMap")]
    public async Task The_destinations_that_need_nothing_looked_up_open_straight_away(string url, string expected)
    {
        using var context = new OpeningContext();

        var outcome = await context.Opener.OpenAsync(url);

        Assert.Equal(NotificationOpenOutcome.Opened, outcome);
        Assert.Equal(expected, context.Navigator.LastDestination);
    }

    [Fact]
    public async Task A_destination_this_build_does_not_know_goes_nowhere_without_failing()
    {
        using var context = new OpeningContext();

        var outcome = await context.Opener.OpenAsync("/something-added-after-this-build");

        Assert.Equal(NotificationOpenOutcome.NowhereToGo, outcome);
        Assert.Empty(context.Navigator.Destinations);
    }

    private sealed class OpeningContext : IDisposable
    {
        private readonly LocalStore _localStore = new();
        private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-08-26T10:00:00Z"));
        private readonly FakeChatServer _chatServer;
        private readonly FakeUsersServer _users = new();
        private readonly ChatRepository _repository;
        private readonly ChatSynchronizer _synchronizer;
        private readonly FakeTasksServer _tasksServer;
        private readonly LocalTaskListRepository _taskLists;
        private readonly TaskListSynchronizer _taskListSynchronizer;
        private readonly Guid _ownUserId = Guid.NewGuid();

        public OpeningContext()
        {
            _chatServer = new FakeChatServer(_clock) { CallerUserId = _ownUserId };

            var keyStorage = new InMemoryChatKeyStorage();
            var vectors = BrowserVectorsFile.Read();
            using (var own = ChatIdentity.FromBackup(vectors.Alice.Backup, vectors.BackupPassword)!)
            {
                keyStorage.WritePrivateKeyJwkAsync(_ownUserId, own.ExportPrivateKeyJwk()).GetAwaiter().GetResult();
            }

            var sessionStore = new SessionStore(new InMemorySessionStorage(
                new UserSession("access", "refresh", _ownUserId, "me@orbit.example", "Me")));
            var encryptionKeyProvider = new OwnEncryptionKeyProvider(
                keyStorage, new EncryptionKeyClient(new FakeEncryptionKeyServer().ToHttpClient()),
                sessionStore, NullLogger<OwnEncryptionKeyProvider>.Instance);

            _repository = new ChatRepository(_localStore, _clock);
            var chatClient = new ChatClient(_chatServer.ToHttpClient());
            var usersClient = new UsersClient(_users.ToHttpClient());
            var sender = new EncryptedChatMessageSender(
                _repository, chatClient, new ChatDirectoryReader(chatClient, usersClient, sessionStore),
                encryptionKeyProvider, new SyncGate(), NullLogger<EncryptedChatMessageSender>.Instance);
            _synchronizer = new ChatSynchronizer(
                _repository, chatClient, usersClient, sender, NullLogger<ChatSynchronizer>.Instance);

            _tasksServer = new FakeTasksServer(_clock);
            _taskLists = new LocalTaskListRepository(
                _localStore, _clock, FixedNetworkStatus.Online, PrivateContent.WithoutAKey());
            _taskListSynchronizer = new TaskListSynchronizer(
                _localStore, new TasksClient(_tasksServer.ToHttpClient()), _clock, new SyncGate(),
                NullLogger<TaskListSynchronizer>.Instance);

            Opener = new NotificationOpener(
                _repository, _synchronizer, usersClient, _taskLists, _taskListSynchronizer,
                new PendingNotificationTap(), Navigator);
        }

        public NotificationOpener Opener { get; }

        public RecordingScreenNavigator Navigator { get; } = new();

        public void GoOffline()
        {
            _chatServer.IsUnreachable = true;
            _users.IsUnreachable = true;
            _tasksServer.IsUnreachable = true;
        }

        /// <summary>A list this phone has already pulled down, as it is stored here.</summary>
        public async Task<LocalTaskList> AddKnownTaskListAsync(string title)
        {
            var serverId = AddTaskListOnTheServerOnly(title);
            await _taskListSynchronizer.SynchroniseAsync();
            return (await _taskLists.GetAllAsync()).Single(taskList => taskList.ServerId == serverId);
        }

        /// <summary>A list the server knows about and this phone does not - the notification arrives first.</summary>
        public Guid AddTaskListOnTheServerOnly(string title) => _tasksServer.AddTaskList(title, isShared: true).Id;

        /// <summary>Somebody this phone has already pulled into its contact list.</summary>
        public async Task<Guid> AddKnownContactAsync(string displayName)
        {
            var userId = AddContactOnTheServerOnly(displayName);
            await _synchronizer.SynchroniseContactsAsync();
            return userId;
        }

        /// <summary>Somebody the server knows about and this phone does not - the notification arrives first.</summary>
        public Guid AddContactOnTheServerOnly(string displayName)
        {
            var userId = AddStranger(displayName);
            var vectors = BrowserVectorsFile.Read();
            _chatServer.AddContact(userId, vectors.Bob.PublicKeyBase64);
            _chatServer.Contacts[^1] = _chatServer.Contacts[^1] with { DisplayName = displayName };
            return userId;
        }

        /// <summary>
        /// An account that exists but is nobody's contact. The server only counts somebody as a contact
        /// once there has been a conversation, so this is the shape a shared note's notification has.
        /// </summary>
        public Guid AddStranger(string displayName)
        {
            var userId = Guid.NewGuid();
            _users.Add(userId, displayName, BrowserVectorsFile.Read().Bob.PublicKeyBase64);
            return userId;
        }

        public async Task<Guid> AddKnownGroupAsync(string name)
        {
            var groupId = AddGroupOnTheServerOnly(name);
            await _synchronizer.SynchroniseGroupsAsync();
            return groupId;
        }

        public Guid AddGroupOnTheServerOnly(string name) => _chatServer.AddGroup(name).Id;

        public void Dispose() => _localStore.Dispose();
    }
}
