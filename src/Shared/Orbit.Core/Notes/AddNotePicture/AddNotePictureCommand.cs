using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.AddNotePicture;

/// <summary>
/// A picture uploaded for a note. The bytes arrive as a stream and are written straight to the store,
/// so a photograph never sits whole in memory on a server that runs at half a gigabyte.
/// </summary>
/// <param name="IsSealed">
/// Whether the bytes are ciphertext the client sealed - which a private note's <em>must</em> be, and
/// which the handler refuses otherwise rather than storing a private note's picture in the clear.
/// </param>
[ClientAction(ClientActionCategory.Edit)]
public sealed record AddNotePictureCommand(
    Guid UserId, Guid NoteId, Stream Content, long SizeBytes, string? ContentType, bool IsSealed)
    : IRequest<AddNotePictureOutcome>;

/// <summary>
/// What became of an upload. Each refusal is its own answer because each is a different sentence to the
/// reader: a note that is not theirs to change, a note already holding all it may, a private note's
/// picture that arrived unsealed.
/// </summary>
public sealed record AddNotePictureOutcome(AddNotePictureOutcomeKind Kind, NotePicture? Picture = null)
{
    public static AddNotePictureOutcome NotFound { get; } = new(AddNotePictureOutcomeKind.NotFound);

    public static AddNotePictureOutcome ReadOnly { get; } = new(AddNotePictureOutcomeKind.ReadOnly);

    public static AddNotePictureOutcome TooLarge { get; } = new(AddNotePictureOutcomeKind.TooLarge);

    public static AddNotePictureOutcome MustBeSealed { get; } = new(AddNotePictureOutcomeKind.MustBeSealed);

    public static AddNotePictureOutcome Added(NotePicture picture) => new(AddNotePictureOutcomeKind.Added, picture);
}

public enum AddNotePictureOutcomeKind
{
    Added,
    NotFound,
    ReadOnly,

    /// <summary>The note would hold more than <see cref="NotePictureLimits.MaximumBytesPerNote"/> with this one.</summary>
    TooLarge,

    /// <summary>A private note's picture arrived in the clear - see Note.IsPrivate, which seals the note itself the same way.</summary>
    MustBeSealed
}
