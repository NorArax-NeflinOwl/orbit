using System.Text.Json;
using Orbit.Contracts.Notes;

namespace Orbit.Web.Services;

/// <summary>
/// What has been written in a note and not saved, kept while this tab lives.
///
/// The editor draws a column of the account's notes beside the writing, and pressing one opens it in
/// place of this one. That used to throw away whatever had not been saved - which is a page quietly
/// deciding that moving your eyes to the next note means abandoning this one. Asked for on 2026-09-18:
/// the writing is kept, comes back when the note is opened again, and leaving the editor altogether is
/// the one thing that asks first, naming the notes that would be lost.
///
/// In memory and nowhere else. A draft is somebody's unfinished writing about something they have not
/// decided to keep; putting it in localStorage would leave it on the machine long after they closed the
/// tab, and a private note's lines are exactly the kind of thing that must not be written down where
/// nothing seals them.
/// </summary>
public sealed class NoteDrafts
{
    /// <summary>
    /// A note as it is being written: everything the editor's form can change, and the name to say it
    /// by. The name is stored rather than read off the lines so a warning can name a note whose first
    /// line has just been emptied.
    /// </summary>
    public sealed record Draft(
        string Name,
        IReadOnlyList<NoteContentLineDto> Lines,
        bool IsPrivate,
        string Priority,
        Guid? FolderId,
        IReadOnlyList<string> Tags);

    private readonly Dictionary<Guid, Draft> _byNoteId = [];

    /// <summary>Raised whenever what is unsaved changes, so a page showing it can say so.</summary>
    public event Action? Changed;

    public bool HasAny => _byNoteId.Count > 0;

    /// <summary>What every unsaved note is called, in the order they were left - what a warning names.</summary>
    public IReadOnlyList<string> Names => [.. _byNoteId.Values.Select(draft => draft.Name)];

    public Draft? For(Guid noteId) => _byNoteId.TryGetValue(noteId, out var draft) ? draft : null;

    /// <summary>
    /// Keeps what is written in one note, or drops it where it says nothing the stored note does not -
    /// so a note opened, read and left behind leaves nothing to warn about.
    /// </summary>
    public void Keep(Guid noteId, Draft written, Draft asStored)
    {
        if (Reads(written) == Reads(asStored))
        {
            Forget(noteId);
            return;
        }

        _byNoteId[noteId] = written;
        Changed?.Invoke();
    }

    public void Forget(Guid noteId)
    {
        if (_byNoteId.Remove(noteId))
        {
            Changed?.Invoke();
        }
    }

    /// <summary>Everything goes: the reader was warned and left anyway, or they signed out.</summary>
    public void ForgetEverything()
    {
        if (_byNoteId.Count == 0)
        {
            return;
        }

        _byNoteId.Clear();
        Changed?.Invoke();
    }

    /// <summary>
    /// What a draft says, as one string, which is how two of them are compared.
    ///
    /// Written out rather than compared field by field: a line carries lists of its own - the marks over
    /// its words, a table's rows, a picture - and a record holding a list compares those by reference, so
    /// two notes that say exactly the same thing would read as different every time. A note is a few
    /// kilobytes, and this runs when one is left rather than as it is typed.
    /// </summary>
    private static string Reads(Draft draft)
        => JsonSerializer.Serialize(draft with { Name = string.Empty });
}
