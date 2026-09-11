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
using Orbit.Mobile.Localization;

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

    /// <summary>
    /// The past of a group is not obviously the newcomer's, so it is handed over only when asked for -
    /// the same default Orbit.Web's checkbox has, and the reason it is a question at all.
    /// </summary>
    [Fact]
    public async Task Adding_somebody_hands_over_nothing_unless_the_switch_says_to()
    {
        using var context = new GroupContext();
        context.AddContact("Celina");
        var screen = await context.OpenGroupAsync("Trip");

        await screen.StartAddingCommand.ExecuteAsync(null);
        screen.Candidates.Single().IsSelected = true;
        await screen.AddSelectedCommand.ExecuteAsync(null);

        Assert.False(screen.ShareHistoryWithNewMembers);
        Assert.Empty(context.Server.HistoryHandedOver);
    }

    /// <summary>
    /// And when it does say to, what came of it is said out loud - a silent nothing looks exactly like a
    /// switch that was never read. This group has nothing anybody could open, which is the answer the
    /// message has to carry rather than swallow.
    /// </summary>
    [Fact]
    public async Task Asking_for_the_history_says_what_came_of_it()
    {
        using var context = new GroupContext();
        context.AddContact("Celina");
        var screen = await context.OpenGroupAsync("Trip");

        await screen.StartAddingCommand.ExecuteAsync(null);
        screen.Candidates.Single().IsSelected = true;
        screen.ShareHistoryWithNewMembers = true;
        await screen.AddSelectedCommand.ExecuteAsync(null);

        // Still added, whatever became of the hand-off: the two are deliberately not one step.
        Assert.Contains(screen.Members, member => member.DisplayName == "Celina");
        Assert.NotEqual(string.Empty, screen.Message);
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

        // Leaving is removing yourself - here with somebody already promoted to run the group after.
        await screen.RemoveCommand.ExecuteAsync(screen.Members.Single(member => member.IsSelf));
        Assert.Equal("ShowGroups", context.Navigator.LastDestination);
    }

    /// <summary>
    /// The only admin can leave straight away, without promoting anybody first - they are asked who takes
    /// over, and confirming with the choice left alone hands the group to its longest-standing member.
    /// This used to come back as a refusal, which left the one person with the most say unable to get out.
    /// </summary>
    [Fact]
    public async Task The_only_admin_can_leave_without_promoting_anybody_first()
    {
        using var context = new GroupContext();
        var celina = context.AddContact("Celina");
        var screen = await context.OpenGroupAsync("Trip", withMembers: [celina]);

        await screen.RemoveCommand.ExecuteAsync(screen.Members.Single(member => member.IsSelf));

        Assert.Equal("ShowGroups", context.Navigator.LastDestination);
        Assert.Equal("Admin", context.Server.Groups.Single().Members.Single().Role);
    }

    /// <summary>
    /// The only admin leaving people behind is asked who takes over, and the person already chosen is the
    /// one the server would pick by itself - the longest-standing, who is not first by name here, so a
    /// choice that defaulted to the alphabet would fail. Saying no leaves nobody.
    /// </summary>
    [Fact]
    public async Task The_only_admin_is_asked_who_takes_over_with_the_longest_standing_member_chosen()
    {
        using var context = new GroupContext();
        var zenon = context.AddContact("Zenon");
        var ada = context.AddContact("Ada");
        context.AnswerToLeaving = _ => false;
        var screen = await context.OpenGroupAsync("Trip", withMembers: [zenon], joiningLater: [ada]);

        await screen.RemoveCommand.ExecuteAsync(screen.Members.Single(member => member.IsSelf));

        var question = Assert.Single(context.Questions);
        Assert.True(question.MustChooseSuccessor);
        Assert.Equal(zenon, question.ChosenSuccessorUserId);
        Assert.Equal(new[] { "Zenon", "Ada" }, question.Candidates.Select(candidate => candidate.DisplayName));
        Assert.Empty(context.Server.GroupsLeft);
        Assert.NotEqual("ShowGroups", context.Navigator.LastDestination);
    }

    [Fact]
    public async Task The_chosen_successor_is_sent_and_takes_over()
    {
        using var context = new GroupContext();
        var zenon = context.AddContact("Zenon");
        var ada = context.AddContact("Ada");
        context.AnswerToLeaving = question =>
        {
            question.ChosenSuccessorUserId = ada;
            return true;
        };
        var screen = await context.OpenGroupAsync("Trip", withMembers: [zenon], joiningLater: [ada]);

        await screen.RemoveCommand.ExecuteAsync(screen.Members.Single(member => member.IsSelf));

        Assert.Equal(ada, Assert.Single(context.Server.SuccessorsNamed));
        var group = Assert.Single(context.Server.Groups);
        Assert.Equal("Admin", group.Members.Single(member => member.UserId == ada).Role);
        Assert.Equal("Member", group.Members.Single(member => member.UserId == zenon).Role);
        Assert.Equal("ShowGroups", context.Navigator.LastDestination);
    }

    /// <summary>The last person out is told the group goes with them - and it does.</summary>
    [Fact]
    public async Task The_last_one_out_is_told_the_group_goes_with_them()
    {
        using var context = new GroupContext();
        var screen = await context.OpenGroupAsync("Trip");

        await screen.RemoveCommand.ExecuteAsync(screen.Members.Single(member => member.IsSelf));

        var question = Assert.Single(context.Questions);
        Assert.True(question.IsLastOneOut);
        Assert.False(question.MustChooseSuccessor);
        Assert.Contains("the group is deleted when you go", question.Message);
        Assert.Empty(context.Server.Groups);
        Assert.Equal("ShowGroups", context.Navigator.LastDestination);
    }

    /// <summary>Everybody is asked to confirm, and a plain member is asked nothing more - and names nobody.</summary>
    [Fact]
    public async Task A_plain_member_is_asked_only_to_confirm()
    {
        using var context = new GroupContext();
        var celina = context.AddContact("Celina");
        var screen = await context.OpenGroupAsync("Trip", withMembers: [celina], ownRole: "Member");

        await screen.RemoveCommand.ExecuteAsync(screen.Members.Single(member => member.IsSelf));

        var question = Assert.Single(context.Questions);
        Assert.False(question.MustChooseSuccessor);
        Assert.False(question.IsLastOneOut);
        Assert.Equal(
            "Leave this group? Your copies of its messages go with you, and only an admin can add you back.",
            question.Message);
        Assert.Null(Assert.Single(context.Server.SuccessorsNamed));
    }

    /// <summary>
    /// The person chosen has left in the meantime. The server refuses rather than handing the group to
    /// somebody else, and the screen says so in its words, stays, and reads the group again so the next
    /// question offers who is actually there.
    /// </summary>
    [Fact]
    public async Task A_refused_leave_is_said_in_the_servers_words_and_the_reader_stays()
    {
        using var context = new GroupContext();
        var zenon = context.AddContact("Zenon");
        var ada = context.AddContact("Ada");
        context.AnswerToLeaving = question =>
        {
            question.ChosenSuccessorUserId = ada;
            context.LeavesInTheMeantime(ada);
            return true;
        };
        var screen = await context.OpenGroupAsync("Trip", withMembers: [zenon], joiningLater: [ada]);

        await screen.RemoveCommand.ExecuteAsync(screen.Members.Single(member => member.IsSelf));

        Assert.Contains("isn't in this group any more", screen.Message);
        Assert.Empty(context.Server.GroupsLeft);
        Assert.NotEqual("ShowGroups", context.Navigator.LastDestination);
        Assert.Contains(screen.Members, member => member.IsSelf);
        Assert.DoesNotContain(screen.Members, member => member.DisplayName == "Ada");
    }

    /// <summary>
    /// The fake refuses what ChatGroup.Leave refuses - the leaver named as their own successor, and a plain
    /// member naming anybody at all - and leaves the group as it was. A fake that took these would let a
    /// phone that sent them look right.
    /// </summary>
    [Fact]
    public async Task The_server_refuses_a_successor_it_would_not_accept()
    {
        using var context = new GroupContext();
        var celina = context.AddContact("Celina");
        await context.OpenGroupAsync("Trip", withMembers: [celina]);
        var groupId = context.Server.Groups.Single().Id;

        var yourself = await context.Client.LeaveGroupAsync(groupId, context.OwnUserId);
        Assert.Equal("Choose somebody other than yourself to take over.", yourself.Refusal);

        var stranger = await context.Client.LeaveGroupAsync(groupId, Guid.NewGuid());
        Assert.Equal("The person you chose to take over isn't in this group any more.", stranger.Refusal);

        Assert.Empty(context.Server.GroupsLeft);
        Assert.Equal(2, context.Server.Groups.Single().Members.Count);
    }

    [Fact]
    public async Task The_server_refuses_a_plain_member_who_names_a_successor()
    {
        using var context = new GroupContext();
        var celina = context.AddContact("Celina");
        await context.OpenGroupAsync("Trip", withMembers: [celina], ownRole: "Member");
        var groupId = context.Server.Groups.Single().Id;

        var result = await context.Client.LeaveGroupAsync(groupId, celina);

        Assert.Equal("Only a group admin can choose who takes over.", result.Refusal);
        Assert.Empty(context.Server.GroupsLeft);
    }

    /// <summary>
    /// A plain member decides nothing about anybody else - and everything about themselves. This used
    /// to assert they were offered nothing at all, which is what the screen did and what the server
    /// says is wrong: see ChatGroup.RemoveMember, whose comment records that requiring admin for both
    /// "left an ordinary member with no way out of a group at all". It did here.
    /// </summary>
    [Fact]
    public async Task A_plain_member_decides_nothing_about_anybody_but_themselves()
    {
        using var context = new GroupContext();
        var celina = context.AddContact("Celina");
        var screen = await context.OpenGroupAsync("Trip", withMembers: [celina], ownRole: "Member");

        Assert.False(screen.IsAdmin);
        Assert.All(screen.Members.Where(member => !member.IsSelf), member =>
        {
            Assert.False(member.CanBeRemoved);
            Assert.False(member.CanBePromoted);
            Assert.False(member.CanBeDemoted);
        });

        var self = screen.Members.Single(member => member.IsSelf);
        Assert.True(self.CanBeRemoved);
        Assert.False(self.CanBePromoted);
        Assert.False(self.CanBeDemoted);
    }

    /// <summary>
    /// And can act on it: showing yourself out is anybody's, so the phone leaves the group and goes back
    /// to the list rather than sitting on a screen about a group the reader is no longer in.
    /// </summary>
    [Fact]
    public async Task A_plain_member_can_show_themselves_out()
    {
        using var context = new GroupContext();
        var celina = context.AddContact("Celina");
        var screen = await context.OpenGroupAsync("Trip", withMembers: [celina], ownRole: "Member");

        await screen.RemoveCommand.ExecuteAsync(screen.Members.Single(member => member.IsSelf));

        Assert.Equal("ShowGroups", context.Navigator.LastDestination);
    }

    /// <summary>
    /// Through the leave route, not by removing yourself: only leaving deletes this reader's copies of
    /// what was said in the group, and the removal left them on the server for nobody to read.
    /// </summary>
    [Fact]
    public async Task Leaving_from_the_groups_own_screen_goes_through_the_leave_route()
    {
        using var context = new GroupContext();
        var celina = context.AddContact("Celina");
        var screen = await context.OpenGroupAsync("Trip", withMembers: [celina], ownRole: "Member");

        await screen.RemoveCommand.ExecuteAsync(screen.Members.Single(member => member.IsSelf));

        Assert.Single(context.Server.GroupsLeft);
    }

    /// <summary>
    /// The button on your own row says what it does. "Remove" is what you do to somebody else, and on
    /// the one row where the somebody is you it read as removing a person rather than leaving.
    /// </summary>
    [Fact]
    public async Task Your_own_row_offers_to_leave_rather_than_to_remove()
    {
        using var context = new GroupContext();
        var celina = context.AddContact("Celina");
        var screen = await context.OpenGroupAsync("Trip", withMembers: [celina]);

        var self = screen.Members.Single(member => member.IsSelf);
        var somebodyElse = screen.Members.Single(member => !member.IsSelf);

        Assert.NotEqual(self.RemovalLabel, somebodyElse.RemovalLabel);
        Assert.NotEmpty(self.RemovalLabel);
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
                null!, new SyncGate(), NullLogger<EncryptedChatMessageSender>.Instance);
            _synchronizer = new ChatSynchronizer(
                _repository, _chatClient, usersClient, sender, NullLogger<ChatSynchronizer>.Instance);
        }

        public FakeChatServer Server { get; }

        public RecordingScreenNavigator Navigator { get; } = new();

        public ChatClient Client => _chatClient;

        public Guid OwnUserId => _ownUserId;

        /// <summary>Every question the screen asked before leaving, in order - what the page would have shown.</summary>
        public List<GroupLeaveQuestion> Questions { get; } = [];

        /// <summary>How the reader answers. Yes, with the choice left alone, unless a test says otherwise.</summary>
        public Func<GroupLeaveQuestion, bool> AnswerToLeaving { get; set; } = _ => true;

        /// <summary>Somebody leaves on the server while the question is on screen - the phone has not heard yet.</summary>
        public void LeavesInTheMeantime(Guid userId)
        {
            var group = Server.Groups.Single();
            Server.Groups[0] = group with { Members = [.. group.Members.Where(member => member.UserId != userId)] };
        }

        /// <summary>Somebody this account has a conversation with, so the server will let them be added.</summary>
        public Guid AddContact(string displayName)
        {
            var userId = Guid.NewGuid();
            Server.AddContact(userId, "a-key");
            Server.Contacts[^1] = Server.Contacts[^1] with { DisplayName = displayName, UserName = displayName.ToLowerInvariant() };
            _users.Add(userId, displayName, "a-key");
            return userId;
        }

        /// <param name="joiningLater">
        /// Put in as plain members a day apart, after everybody in withMembers - so who has been there
        /// longest is decided by when they joined rather than by whose id sorts first.
        /// </param>
        public async Task<GroupDetailViewModel> OpenGroupAsync(
            string name, Guid[]? withMembers = null, string ownRole = "Admin", Guid[]? joiningLater = null)
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

            foreach (var userId in joiningLater ?? [])
            {
                _clock.Advance(TimeSpan.FromDays(1));
                Server.AddMember(group.Id, userId);
            }

            await _synchronizer.SynchroniseGroupsAsync();
            var stored = (await _repository.GetGroupsAsync()).Single(candidate => candidate.Id == group.Id);

            var screen = new GroupDetailViewModel(
                _repository, _chatClient, _synchronizer, _sessionStore,
                GroupHistory.SharedBy(_chatClient, _sessionStore, _users),
                new Translations(new InMemoryLanguageStore()), Navigator)
            {
                AskBeforeLeaving = question =>
                {
                    Questions.Add(question);
                    return Task.FromResult(AnswerToLeaving(question));
                }
            };
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
