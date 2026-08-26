using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Contracts.Chat;
using Orbit.Mobile.Api;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Data;
using Orbit.Mobile.Screens.Chat;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// Managing a group's membership from the phone, which until now could only create one and never change
/// it. Every rule belongs to the server; what these pin down is that the screen offers what an admin
/// can actually do, and repeats the server's own words when it refuses anyway.
/// </summary>
public sealed class GroupDetailScreenTests
{
    [Fact]
    public async Task An_admin_can_add_somebody_they_already_have_a_conversation_with()
    {
        using var context = new GroupContext();
        context.AddContact("Celina");
        var screen = await context.OpenGroupAsync("Trip");

        await screen.StartAddingCommand.ExecuteAsync(null);
        screen.Candidates.Single().IsSelected = true;
        await screen.AddSelectedCommand.ExecuteAsync(null);

        Assert.Contains(screen.Members, member => member.DisplayName == "Celina");
        Assert.Equal(string.Empty, screen.Message);
    }

    [Fact]
    public async Task Somebody_already_in_the_group_is_not_offered_again()
    {
        using var context = new GroupContext();
        var celina = context.AddContact("Celina");
        var screen = await context.OpenGroupAsync("Trip", withMembers: [celina]);

        await screen.StartAddingCommand.ExecuteAsync(null);

        // Nobody left to add, so the screen says so rather than opening an empty picker.
        Assert.Empty(screen.Candidates);
        Assert.False(screen.IsAdding);
        Assert.Contains("already in this group", screen.Message);
    }

    [Fact]
    public async Task The_last_admin_cannot_be_demoted_and_is_told_the_rule()
    {
        using var context = new GroupContext();
        var screen = await context.OpenGroupAsync("Trip");

        var self = screen.Members.Single(member => member.IsSelf);
        Assert.True(self.CanBeDemoted);
        await screen.DemoteCommand.ExecuteAsync(self);

        // The server's own wording, not a guess made here - it knows the rule and cannot drift from it.
        Assert.Contains("at least one admin", screen.Message);
        Assert.True(screen.Members.Single(member => member.IsSelf).IsAdmin);
    }

    [Fact]
    public async Task Promoting_somebody_else_then_leaving_works_and_goes_back_to_the_groups()
    {
        using var context = new GroupContext();
        var celina = context.AddContact("Celina");
        var screen = await context.OpenGroupAsync("Trip", withMembers: [celina]);

        await screen.PromoteCommand.ExecuteAsync(screen.Members.Single(member => member.DisplayName == "Celina"));
        Assert.True(screen.Members.Single(member => member.DisplayName == "Celina").IsAdmin);

        // Leaving is removing yourself, which only works once somebody else can run the group.
        await screen.RemoveCommand.ExecuteAsync(screen.Members.Single(member => member.IsSelf));
        Assert.Equal("ShowGroups", context.Navigator.LastDestination);
    }

    [Fact]
    public async Task A_plain_member_is_offered_nothing_to_change()
    {
        using var context = new GroupContext();
        var celina = context.AddContact("Celina");
        var screen = await context.OpenGroupAsync("Trip", withMembers: [celina], ownRole: "Member");

        Assert.False(screen.IsAdmin);
        Assert.All(screen.Members, member =>
        {
            Assert.False(member.CanBeRemoved);
            Assert.False(member.CanBePromoted);
            Assert.False(member.CanBeDemoted);
        });
    }

    [Fact]
    public async Task Changing_membership_with_no_connection_says_so()
    {
        using var context = new GroupContext();
        var celina = context.AddContact("Celina");
        var screen = await context.OpenGroupAsync("Trip", withMembers: [celina]);

        context.Server.IsUnreachable = true;
        await screen.RemoveCommand.ExecuteAsync(screen.Members.Single(member => member.DisplayName == "Celina"));

        Assert.Contains("connection", screen.Message);
    }

    private sealed class GroupContext : IDisposable
    {
        private readonly LocalStore _localStore = new();
        private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-08-26T10:00:00Z"));
        private readonly FakeUsersServer _users = new();
        private readonly ChatClient _chatClient;
        private readonly ChatSynchronizer _synchronizer;
        private readonly SessionStore _sessionStore;
        private readonly ChatRepository _repository;
        private readonly Guid _ownUserId = Guid.NewGuid();

        public GroupContext()
        {
            Server = new FakeChatServer(_clock) { CallerUserId = _ownUserId };
            _users.Add(_ownUserId, "Me", "own-key");

            var session = new UserSession("access", "refresh", _ownUserId, "me@orbit.example", "Me");
            _sessionStore = new SessionStore(new InMemorySessionStorage(session));
            _repository = new ChatRepository(_localStore, _clock);
            _chatClient = new ChatClient(Server.ToHttpClient());
            var usersClient = new UsersClient(_users.ToHttpClient());
            var sender = new EncryptedChatMessageSender(
                _repository, _chatClient, new ChatDirectoryReader(_chatClient, usersClient, _sessionStore),
                null!, NullLogger<EncryptedChatMessageSender>.Instance);
            _synchronizer = new ChatSynchronizer(
                _repository, _chatClient, usersClient, sender, NullLogger<ChatSynchronizer>.Instance);
        }

        public FakeChatServer Server { get; }

        public RecordingScreenNavigator Navigator { get; } = new();

        /// <summary>Somebody this account has a conversation with, so the server will let them be added.</summary>
        public Guid AddContact(string displayName)
        {
            var userId = Guid.NewGuid();
            Server.AddContact(userId, "a-key");
            Server.Contacts[^1] = Server.Contacts[^1] with { DisplayName = displayName, UserName = displayName.ToLowerInvariant() };
            _users.Add(userId, displayName, "a-key");
            return userId;
        }

        public async Task<GroupDetailViewModel> OpenGroupAsync(
            string name, Guid[]? withMembers = null, string ownRole = "Admin")
        {
            var group = Server.AddGroup(name, withMembers ?? []);
            if (ownRole != "Admin")
            {
                var index = Server.Groups.FindIndex(candidate => candidate.Id == group.Id);
                Server.Groups[index] = group with
                {
                    OwnRole = ownRole,
                    Members = [.. group.Members.Select(member =>
                        member.UserId == _ownUserId ? member with { Role = ownRole } : member with { Role = "Admin" })]
                };
            }

            await _synchronizer.SynchroniseGroupsAsync();
            var stored = (await _repository.GetGroupsAsync()).Single(candidate => candidate.Id == group.Id);

            var screen = new GroupDetailViewModel(_repository, _chatClient, _synchronizer, _sessionStore, Navigator);
            screen.Open(stored);
            await screen.LoadCommand.ExecuteAsync(null);
            return screen;
        }

        public void Dispose()
        {
            Server.Dispose();
            _users.Dispose();
            _localStore.Dispose();
        }
    }
}
