using Orbit.Core.Abstractions;
using Orbit.Core.Folders;
using Orbit.Mobile.Api;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Folders;
using Orbit.Mobile.Screens.Notes;
using Orbit.Mobile.Screens.Sharing;
using Orbit.Mobile.Security;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.Chat;
using Orbit.Mobile.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using SharingOutcome = Orbit.Mobile.Chat.SharingOutcome;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// Choosing several things on a phone's list screen and doing one thing to all of them - the phone's
/// half of what the browser's list pages already did. The notes screen stands in for all four: each
/// hands the same PickingSeveral its own two writes, so what is true here is true on the others.
/// </summary>
public sealed class PickingSeveralTests
{
    /// <summary>Choosing is a mode: outside it a press opens, inside it the same press chooses.</summary>
    [Fact]
    public async Task While_choosing_a_press_on_a_row_chooses_it_instead_of_opening_it()
    {
        using var context = new NotesContext();
        await context.AddNoteAsync("Receipts");
        var screen = await context.OpenAsync();

        screen.ToggleChoosingCommand.Execute(null);
        screen.OpenCommand.Execute(screen.Notes.Single());

        Assert.Null(context.Navigator.LastDestination);
        var row = Assert.Single(screen.Notes);
        Assert.True(row.OffersPicking);
        Assert.True(row.IsPicked);
        Assert.Equal("1 chosen", screen.Picking.Summary);

        screen.Picking.StopCommand.Execute(null);
        Assert.False(Assert.Single(screen.Notes).OffersPicking);
        screen.OpenCommand.Execute(screen.Notes.Single());
        Assert.NotNull(context.Navigator.LastDestination);
    }

    /// <summary>
    /// Filed together, and what has left the folder being read stops counting - the bar counting notes
    /// nobody can see would act on them with the next press.
    /// </summary>
    [Fact]
    public async Task Filing_the_chosen_notes_moves_every_one_and_forgets_what_left_the_screen()
    {
        using var context = new NotesContext();
        await context.AddNoteAsync("Receipts");
        await context.AddNoteAsync("Invoices");
        await context.AddNoteAsync("Recipes");
        var screen = await context.OpenAsync();
        var work = await context.Folders.CreateAsync("Work", FolderScope.Notes);

        screen.ToggleChoosingCommand.Execute(null);
        screen.OpenCommand.Execute(screen.Notes.Single(row => row.Title == "Receipts"));
        screen.OpenCommand.Execute(screen.Notes.Single(row => row.Title == "Invoices"));
        await screen.Picking.FileCommand.ExecuteAsync(work.LocalId);

        Assert.Equal(["Recipes"], screen.Notes.Select(row => row.Title));
        Assert.False(screen.Picking.HasAny);
        Assert.True(screen.Picking.IsPicking);

        screen.ChooseFolderCommand.Execute(FolderKey.Of(work.LocalId));
        Assert.Equal(["Invoices", "Receipts"], screen.Notes.Select(row => row.Title).Order());
    }

    /// <summary>One button, named for what it will do: Put back once everything chosen is away.</summary>
    [Fact]
    public async Task Putting_the_chosen_away_and_back_is_one_button()
    {
        using var context = new NotesContext();
        await context.AddNoteAsync("Receipts");
        await context.AddNoteAsync("Invoices");
        var screen = await context.OpenAsync();

        screen.ToggleChoosingCommand.Execute(null);
        screen.Picking.ToggleAllCommand.Execute(null);
        Assert.Equal("Archive", screen.Picking.ArchiveName);
        await screen.Picking.ArchiveCommand.ExecuteAsync(null);

        Assert.Empty(screen.Notes);
        screen.ChooseFolderCommand.Execute(FolderKey.Of(BuiltInFolder.Archived));
        screen.Picking.ToggleAllCommand.Execute(null);
        Assert.Equal("Put back", screen.Picking.ArchiveName);

        await screen.Picking.ArchiveCommand.ExecuteAsync(null);

        Assert.Empty(screen.Notes);
        screen.ChooseFolderCommand.Execute(FolderKey.Default);
        Assert.Equal(2, screen.Notes.Count);
    }

    /// <summary>"All of them" is one press meaning both things, as it is in the browser.</summary>
    [Fact]
    public void All_of_them_chooses_everything_on_the_screen_and_then_nothing()
    {
        var picking = new PickingSeveral(Translations(), Doing.Nothing);
        picking.Start();
        picking.Shows([Thing("Receipts"), Thing("Invoices")]);

        picking.ToggleAllCommand.Execute(null);
        Assert.Equal("2 chosen", picking.Summary);

        picking.ToggleAllCommand.Execute(null);
        Assert.False(picking.HasAny);
    }

    /// <summary>Somebody else's is left as it is, and the round says how many were.</summary>
    [Fact]
    public async Task Somebody_elses_is_left_alone_and_counted()
    {
        List<Guid> archived = [];
        var picking = new PickingSeveral(Translations(), Doing.Nothing with
        {
            Archive = (localId, _, _) =>
            {
                archived.Add(localId);
                return Task.FromResult(LocalWriteOutcome.Applied);
            }
        });
        var mine = Thing("Receipts");
        var theirs = Thing("Their list") with { IsSomebodyElses = true };
        picking.Start();
        picking.Shows([mine, theirs]);
        picking.ToggleAllCommand.Execute(null);

        await picking.ArchiveCommand.ExecuteAsync(null);

        Assert.Equal([mine.LocalId], archived);
        Assert.Equal("1 of the chosen are somebody else's, so they were left as they are.", picking.Message);
    }

    /// <summary>
    /// Offered one at a time to one contact, each with its own invitation. What cannot be offered - sealed,
    /// somebody else's, or never sent to the server - is left out and counted rather than silently dropped.
    /// </summary>
    [Fact]
    public async Task Sharing_offers_each_that_can_be_offered_and_counts_the_rest()
    {
        using var chat = new ChatContext();
        using var shares = new FakeShareServer();
        var contact = await GiveAContactAsync(chat, "Anna");
        var picking = new PickingSeveral(
            Translations(), Doing.Nothing with { Kind = SharedItemKind.Note }, Sharing(chat, shares));
        picking.Start();
        picking.Shows(
        [
            Thing("Receipts"),
            Thing("Invoices"),
            Thing("Diary") with { IsPrivate = true },
            Thing("Not sent yet") with { ServerId = null }
        ]);
        picking.ToggleAllCommand.Execute(null);

        Assert.Equal(["Anna"], (await picking.ContactsAsync()).Select(candidate => candidate.DisplayName));
        await picking.ShareAsync(contact, ShareAccessLevel.ReadOnly);

        Assert.Equal(2, shares.Accepted.Count(path => path.StartsWith("api/notes/") && path.EndsWith("/shares")));
        Assert.Equal(2, chat.Server.Messages.Count);
        Assert.Equal(
            "Shared 2 - they'll see them in your chat. 2 of the chosen can't be shared - private, somebody else's, or not on the server yet.",
            picking.Message);
    }

    /// <summary>Only where this account may share at all, and never without a way to share.</summary>
    [Fact]
    public void Sharing_is_not_offered_without_the_means_to_share()
    {
        var picking = new PickingSeveral(Translations(), Doing.Nothing);

        Assert.False(picking.CanShare);
    }

    private static Translations Translations() => new(new InMemoryLanguageStore());

    private static PickableThing Thing(string name)
        => new(Guid.NewGuid(), Guid.NewGuid(), name, IsSomebodyElses: false, IsPrivate: false, IsArchived: false);

    private static class Doing
    {
        public static PickingActions Nothing { get; } = new(
            SharedItemKind.Note,
            (_, _, _) => Task.FromResult(LocalWriteOutcome.Applied),
            (_, _, _) => Task.FromResult(LocalWriteOutcome.Applied),
            _ => Task.CompletedTask,
            () => []);
    }

    private static SharingSeveral Sharing(ChatContext chat, FakeShareServer shares)
    {
        var http = shares.ToHttpClient();
        return new SharingSeveral(
            chat.Repository,
            chat.Synchronizer,
            new SharedItemSharing(
                new NotesClient(http), new TasksClient(http), new CalendarClient(http), new InventoryClient(http),
                new PlacesClient(http), chat.Sender),
            UnlockedPermissions.For(new LocalStore()));
    }

    /// <inheritdoc cref="SharePanelTests"/>
    private static async Task<LocalContact> GiveAContactAsync(ChatContext context, string displayName)
    {
        context.GiveTheOtherPartyAPublishedKey();
        var index = context.Server.Contacts.FindIndex(candidate => candidate.UserId == context.OtherUserId);
        context.Server.Contacts[index] = context.Server.Contacts[index] with { DisplayName = displayName };
        await context.Repository.StoreContactsAsync([context.Server.Contacts[index]]);
        return (await context.Repository.GetContactsAsync()).Single();
    }

    private sealed class NotesContext : IDisposable
    {
        private readonly LocalStore _localStore = new();
        private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-09-16T10:00:00Z"));
        private readonly FakeNotesServer _server;
        private readonly NoteSynchronizer _synchronizer;

        public NotesContext()
        {
            _server = new FakeNotesServer(_clock);
            Notes = new LocalNoteRepository(
                _localStore, _clock, FixedNetworkStatus.Online, PrivateContent.HoldingAKeyFor(Guid.NewGuid()));
            Folders = new LocalFolderRepository(_localStore, _clock);
            _synchronizer = new NoteSynchronizer(
                _localStore, new NotesClient(_server.ToHttpClient()), _clock, new SyncGate(),
                NullLogger<NoteSynchronizer>.Instance);
        }

        public LocalNoteRepository Notes { get; }

        public LocalFolderRepository Folders { get; }

        public RecordingScreenNavigator Navigator { get; } = new();

        public Task<LocalNote> AddNoteAsync(string title)
            => Notes.CreateAsync(title, [new Orbit.Contracts.Notes.NoteContentLineDto("milk", false, false)]);

        public async Task<NotesViewModel> OpenAsync()
        {
            var screen = new NotesViewModel(
                Notes, _synchronizer, new NotesClient(_server.ToHttpClient()), FixedNetworkStatus.Online,
                new Translations(new InMemoryLanguageStore()),
                new PrivateItemGate(new FixedDeviceAuthentication()),
                new SyncState(Reachability.Online, _clock), Navigator, _clock,
                new InMemoryListArrangementStore(), Folders, new InMemoryChosenFolderStore(),
                TestDoubles.Folders.SynchronizerAgainstNobody(_localStore, _clock));

            await screen.LoadCommand.ExecuteAsync(null);
            return screen;
        }

        public void Dispose()
        {
            _server.Dispose();
            _localStore.Dispose();
        }
    }
}
