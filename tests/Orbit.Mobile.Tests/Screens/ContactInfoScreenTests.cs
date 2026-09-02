using Microsoft.Extensions.Time.Testing;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Chat;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// Who somebody is, on one screen - the card Orbit.Web opens at /contacts/{id} and the phone had no
/// answer for at all: from the phone, a name in a list was all there was of a person.
///
/// It answers from what the phone already holds first. A card that says nothing without a connection is
/// blank exactly when somebody is looking up who they are talking to on a train.
/// </summary>
public sealed class ContactInfoScreenTests
{
    [Fact]
    public async Task It_says_who_somebody_is_from_what_the_phone_holds()
    {
        using var context = new CardContext();
        var stored = await context.StoreContactAsync(
            "ada", "Ada Lovelace", "ada@orbit.example", presence: "Away", hasKey: true);

        var card = context.Open(stored.UserId);
        await card.LoadCommand.ExecuteAsync(null);

        Assert.Equal("Ada Lovelace", card.DisplayName);
        Assert.Equal("@ada", card.UserName);
        Assert.Equal("ada@orbit.example", card.Email);
        Assert.True(card.HasEmail);
        Assert.Equal("Away", card.PresenceStatus);
        Assert.True(card.HasLastMessage);
    }

    /// <summary>
    /// The card is read where there is no connection to read it over, which is the point of holding the
    /// row on the phone at all.
    /// </summary>
    [Fact]
    public async Task Offline_it_still_says_what_it_knows()
    {
        using var context = new CardContext();
        var stored = await context.StoreContactAsync("ada", "Ada Lovelace", "ada@orbit.example");
        context.NobodyCanBeReached();

        var card = context.Open(stored.UserId);
        await card.LoadCommand.ExecuteAsync(null);

        Assert.Equal("Ada Lovelace", card.DisplayName);
        Assert.Equal("ada@orbit.example", card.Email);
        // Nothing was learned, and nothing is claimed: being offline is not an answer about somebody.
        Assert.False(card.HasMessage);
    }

    /// <summary>Nobody can be written to until they have made themselves a key.</summary>
    [Fact]
    public async Task It_says_when_somebody_cannot_be_written_to_yet()
    {
        using var context = new CardContext();
        var stored = await context.StoreContactAsync("ada", "Ada Lovelace", "ada@orbit.example", hasKey: false);

        var card = context.Open(stored.UserId);
        await card.LoadCommand.ExecuteAsync(null);

        Assert.True(card.HasMessage);
        Assert.Contains("Ada Lovelace", card.Message);
    }

    [Fact]
    public async Task It_says_what_is_waiting_on_an_answer()
    {
        using var context = new CardContext();
        var stored = await context.StoreContactAsync(
            "ada", "Ada Lovelace", "ada@orbit.example", requiresApproval: true);

        var card = context.Open(stored.UserId);
        await card.LoadCommand.ExecuteAsync(null);

        Assert.True(card.IsWaitingOnSomebody);
    }

    /// <summary>
    /// A conversation outlives the profile behind it: an account that has made itself unfindable answers
    /// as nobody does, and the words already said are unaffected - which is what the card says.
    /// </summary>
    [Fact]
    public async Task A_contact_whose_account_cannot_be_looked_up_keeps_their_row()
    {
        using var context = new CardContext();
        var stored = await context.StoreContactAsync("ada", "Ada Lovelace", "ada@orbit.example");
        context.NobodyResolves();

        var card = context.Open(stored.UserId);
        await card.LoadCommand.ExecuteAsync(null);

        Assert.Equal("Ada Lovelace", card.DisplayName);
        Assert.Contains("still readable", card.Message);
    }

    /// <summary>Somebody found by search, who there is no conversation with yet.</summary>
    [Fact]
    public async Task A_stranger_has_no_conversation_to_open()
    {
        using var context = new CardContext();
        var stranger = context.AddAccount("Grace Hopper");

        var card = context.Open(stranger);
        await card.LoadCommand.ExecuteAsync(null);

        Assert.Equal("Grace Hopper", card.DisplayName);
        Assert.False(card.IsConversationOffered);
    }

    [Fact]
    public async Task Opening_the_conversation_goes_to_it()
    {
        using var context = new CardContext();
        var stored = await context.StoreContactAsync("ada", "Ada Lovelace", "ada@orbit.example");
        var card = context.Open(stored.UserId);
        await card.LoadCommand.ExecuteAsync(null);

        Assert.True(card.IsConversationOffered);
        card.OpenConversationCommand.Execute(null);

        Assert.Equal("ShowConversation", context.Navigator.LastDestination);
        Assert.Equal(stored.UserId, context.Navigator.LastContact!.UserId);
    }

    private sealed class CardContext : IDisposable
    {
        private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 9, 2, 8, 0, 0, TimeSpan.Zero));
        private readonly LocalStore _localStore = new();
        private ChatRepository Chat => new(_localStore, _clock);

        public FakeUsersServer Users { get; } = new();

        public RecordingScreenNavigator Navigator { get; } = new();

        /// <summary>An account the server knows about, and this phone may or may not have spoken to.</summary>
        public Guid AddAccount(string displayName, string? userName = null)
        {
            var userId = Guid.NewGuid();
            Users.Add(userId, displayName, "a-key", userName);
            return userId;
        }

        /// <summary>Somebody this phone has spoken to, as the sync would have left them.</summary>
        public async Task<LocalContact> StoreContactAsync(
            string userName, string displayName, string email, string presence = "Available",
            bool hasKey = true, bool requiresApproval = false)
        {
            var userId = AddAccount(displayName, userName);
            await Chat.StoreContactsAsync([
                new Orbit.Contracts.Chat.ContactDto(
                    userId, userName, displayName, email, hasKey ? "a-key" : null, _clock.GetUtcNow(),
                    requiresApproval, false, PresenceStatus: presence)
            ]);

            return (await Chat.GetContactsAsync()).Single(contact => contact.UserId == userId);
        }

        /// <summary>The card opened for one account - see ContactInfoViewModel.</summary>
        public ContactInfoViewModel Open(Guid userId)
        {
            var card = new ContactInfoViewModel(
                Chat, new UsersClient(Users.ToHttpClient()),
                UnlockedPermissions.For(_localStore), new Translations(new InMemoryLanguageStore()), Navigator);

            card.Open(userId);
            return card;
        }

        /// <summary>No server at all, which is the case the stored row exists for.</summary>
        public void NobodyCanBeReached() => Users.IsUnreachable = true;

        /// <summary>
        /// A server that answers and has never heard of them - an account that made itself unfindable
        /// answers exactly as one that never existed.
        /// </summary>
        public void NobodyResolves() => Users.ForgetEverybody();

        public void Dispose()
        {
            Users.Dispose();
            _localStore.Dispose();
        }
    }
}
