using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Notes;
using Orbit.Core.Notes.GetNoteById;
using Orbit.Core.Notes.GetNotes;
using Orbit.Core.Users;
using Xunit;

namespace Orbit.Api.Tests.Notes;

/// <summary>
/// An owner could always see who a note was shared *with* by asking for its share status; what nothing
/// exposed was the plain fact that a note is shared out at all, on the note itself. The mobile client
/// needs it on every note in a list: it cannot hold an edit lock, so anything another person can change
/// has to be read-only while offline (info/orbit-maui-plan.md §5.4). Without this an owner's copy of a
/// shared note is indistinguishable from a private one, and the policy has nothing to act on.
/// </summary>
public sealed class SharedWithOthersTests
{
    [Fact]
    public async Task An_owner_sees_that_a_note_somebody_accepted_is_shared_out()
    {
        var context = new SharingContext();
        var note = await context.AddOwnedNoteAsync("Groceries");
        await context.ShareAsync(note, accepted: true);

        var notes = await context.ListForOwnerAsync();

        Assert.True(Assert.Single(notes).IsSharedWithOthers);
    }

    [Fact]
    public async Task An_offer_nobody_has_accepted_yet_does_not_count_as_shared_out()
    {
        var context = new SharingContext();
        var note = await context.AddOwnedNoteAsync("Groceries");
        await context.ShareAsync(note, accepted: false);

        var notes = await context.ListForOwnerAsync();

        // Nobody can read or change it yet, so there is nothing to keep the owner from editing offline.
        Assert.False(Assert.Single(notes).IsSharedWithOthers);
    }

    [Fact]
    public async Task A_note_nobody_was_offered_is_not_shared_out()
    {
        var context = new SharingContext();
        await context.AddOwnedNoteAsync("Private thoughts");

        var notes = await context.ListForOwnerAsync();

        Assert.False(Assert.Single(notes).IsSharedWithOthers);
    }

    [Fact]
    public async Task Sharing_one_note_does_not_mark_the_owners_other_notes()
    {
        var context = new SharingContext();
        var shared = await context.AddOwnedNoteAsync("Groceries");
        await context.AddOwnedNoteAsync("Private thoughts");
        await context.ShareAsync(shared, accepted: true);

        var notes = await context.ListForOwnerAsync();

        Assert.Single(notes, note => note.IsSharedWithOthers);
        Assert.Single(notes, note => !note.IsSharedWithOthers);
    }

    [Fact]
    public async Task The_flag_is_on_a_single_note_read_too_not_only_the_list()
    {
        var context = new SharingContext();
        var note = await context.AddOwnedNoteAsync("Groceries");
        await context.ShareAsync(note, accepted: true);

        var loaded = await context.GetForOwnerAsync(note.Id);

        Assert.True(loaded!.IsSharedWithOthers);
    }

    [Fact]
    public async Task The_recipient_of_a_share_is_not_told_it_is_shared_out()
    {
        var context = new SharingContext();
        var note = await context.AddOwnedNoteAsync("Groceries");
        await context.ShareAsync(note, accepted: true);

        var asRecipient = Assert.Single(await context.ListForRecipientAsync());

        // The two flags describe opposite ends of one relationship; the recipient's end is IsShared,
        // and conflating them would make every shared note look shared out to everybody.
        Assert.True(asRecipient.IsShared);
        Assert.False(asRecipient.IsSharedWithOthers);
    }

    private sealed class SharingContext
    {
        private readonly InMemoryNoteRepository _notes = new();
        private readonly InMemoryNoteShareRepository _shares = new();
        private readonly InMemoryUserRepository _users = new();
        private readonly User _owner = User.Create("owner@example.com", "owner", "Owner", "hash");

        public Guid RecipientId { get; } = Guid.NewGuid();

        public async Task<Note> AddOwnedNoteAsync(string title)
        {
            await _users.AddAsync(_owner, CancellationToken.None);
            var note = Note.Create(_owner.Id, title, [NoteContentLine.PlainText("Content")]);
            await _notes.AddAsync(note, CancellationToken.None);
            return note;
        }

        public async Task ShareAsync(Note note, bool accepted)
        {
            var share = NoteShare.Create(note.Id, _owner.Id, RecipientId, ShareAccessLevel.CanEdit);
            if (accepted)
            {
                share.MarkAccepted();
            }

            await _shares.AddAsync(share, CancellationToken.None);
        }

        public Task<IReadOnlyList<Note>> ListForOwnerAsync()
            => new GetNotesQueryHandler(Resolver()).HandleAsync(new GetNotesQuery(_owner.Id), CancellationToken.None);

        public Task<IReadOnlyList<Note>> ListForRecipientAsync()
            => new GetNotesQueryHandler(Resolver()).HandleAsync(new GetNotesQuery(RecipientId), CancellationToken.None);

        public Task<Note?> GetForOwnerAsync(Guid noteId)
            => new GetNoteByIdQueryHandler(Resolver()).HandleAsync(new GetNoteByIdQuery(_owner.Id, noteId), CancellationToken.None);

        private NoteAccessResolver Resolver() => new(_notes, _shares, _users);
    }
}
