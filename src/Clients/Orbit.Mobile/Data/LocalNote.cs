using System.ComponentModel.DataAnnotations.Schema;
using Orbit.Contracts;
using Orbit.Contracts.Notes;

namespace Orbit.Mobile.Data;

/// <summary>
/// A note as the phone holds it. Mirrors <see cref="NoteDto"/> plus the bookkeeping the server has no
/// reason to know about - see info/orbit-maui-plan.md §5.1.
///
/// Screens read these rows, never the API directly. That is what makes the app work offline, and it is
/// structural rather than an optimisation: a screen written against the API cannot be given offline
/// support later without rewriting it.
/// </summary>
public sealed class LocalNote : Orbit.Mobile.Sync.ISharedState, ICopyableForEditing
{
    /// <summary>
    /// The key on this device, generated here and never changing. Distinct from <see cref="ServerId"/>
    /// because a note created offline exists before the server has ever seen it, and rows that already
    /// point at it must not have to be found and rewritten once it does.
    /// </summary>
    public Guid LocalId { get; set; }

    /// <summary>The id the server knows this note by. Null until a create has actually been accepted.</summary>
    public Guid? ServerId { get; set; }

    public string Title { get; set; } = string.Empty;

    public IReadOnlyList<NoteContentLineDto> Content { get; set; } = [];

    public bool IsPrivate { get; set; }

    /// <summary>
    /// The sealed title and lines of a private note. This is where a private note's words live: the
    /// readable columns above are empty for one, on the phone exactly as on the server, so a database
    /// file lifted off the handset says no more about it than Orbit.Api can - see PrivateContentSealer.
    /// </summary>
    public string? EncryptedCiphertext { get; set; }

    public string? EncryptedNonce { get; set; }

    /// <summary>The sealed payload in the shape it travels and is opened in, or null when nothing is sealed here.</summary>
    [NotMapped]
    public EncryptedContentDto? EncryptedContent
        => EncryptedCiphertext is { } ciphertext && EncryptedNonce is { } nonce
            ? new EncryptedContentDto(ciphertext, nonce)
            : null;

    /// <summary>
    /// True when this row's private content is still sealed because the read could not open it - this
    /// device holds no key, or the note was sealed under a key pair that has since been replaced. Not
    /// stored: it describes one read rather than the note, and the next read may well answer differently.
    /// </summary>
    [NotMapped]
    public bool IsSealed { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    /// <summary>True when this note reached the phone through somebody else's share.</summary>
    public bool IsShared { get; set; }

    public string? SharedByUserName { get; set; }

    /// <summary>True when the owner shared this note out and another person can change it.</summary>
    public bool IsSharedWithOthers { get; set; }

    public string AccessLevel { get; set; } = "CanEdit";
    /// <summary>
    /// Whoever created it, before any sharing - meaningful only when this arrived through a share. Kept
    /// so somebody holding it read-only can ask them for more: the request is a chat message, and a
    /// message needs somebody to address it to.
    /// </summary>
    public Guid? OwnerUserId { get; set; }

    /// <summary>
    /// Kept at the top of the list. Only the owner sets it - pinning moves a card on one person's page,
    /// so a recipient pinning a note shared with them would be rearranging its owner's list - and it
    /// deliberately does not touch <see cref="UpdatedAtUtc"/>: it changes where a note sits, not what it
    /// says.
    /// </summary>
    public bool IsPinned { get; set; }

    /// <summary>
    /// How much this note matters, by name - "Low", "Normal" or "High", as ItemPriority spells them.
    /// Held because a save sends the whole note: without it every edit made here answered "Normal", and
    /// a note somebody had marked High quietly dropped back the next time it was touched from a phone.
    /// </summary>
    public string Priority { get; set; } = "Normal";

    /// <summary>
    /// When the server last confirmed this row. Null for a note created offline that has never been
    /// accepted - which is also what <see cref="ServerId"/> being null means, kept separately because a
    /// synced note that is later edited offline has one and not the other.
    /// </summary>
    /// <summary>
    /// The note this one is a copy of, made so it could be edited with no connection - null for an
    /// ordinary note, which is nearly all of them.
    ///
    /// Offline, a note somebody else can change is read-only: it is one row, protected by a lock this
    /// phone cannot hold, so an edit made here could only be discovered to be impossible at replay time
    /// (see OfflineEditPolicy). Refusing was honest and unhelpful - somebody on a train with something
    /// to write down has nowhere to put it. So the refusal now offers a copy instead: a note of this
    /// phone's own, which nobody else can be editing and which therefore has no such problem.
    ///
    /// What it costs is the reconciling, which is what <see cref="CopyBaseTitle"/> exists for.
    /// </summary>
    public Guid? CopyOfLocalId { get; set; }

    /// <summary>When the copy was taken, which is what the review screen orders and dates them by.</summary>
    public DateTimeOffset? CopiedAtUtc { get; set; }

    /// <summary>
    /// What the original said at the moment the copy was taken - the third point a review needs.
    ///
    /// Without it a review can only say "these two differ", which is true of every field the reader
    /// deliberately changed and says nothing. With it the screen can tell the two apart: a field only
    /// this copy changed is a change to apply, and one both sides changed is the actual conflict.
    /// </summary>
    public string CopyBaseTitle { get; set; } = string.Empty;

    /// <inheritdoc cref="ICopyableForEditing.CopyBaseLines"/>
    public IReadOnlyList<string> CopyBaseLines { get; set; } = [];

    /// <summary>
    /// Kept on purpose after a review rather than applied or dropped - the reader wanted both versions.
    /// It stops being a copy under review and becomes a note in its own right, still pointing at what it
    /// came from so the History screen can say where it came from.
    /// </summary>
    public bool IsKeptCopy { get; set; }

    public DateTimeOffset? LastSyncedAtUtc { get; set; }
}
