namespace Orbit.Core.Folders;

/// <summary>
/// The folders every account has without having made them: everything is in one of them until it is put
/// somewhere else. They are not rows, and three of them are not stored at all - which one something is
/// in is decided from what it already is, so nothing needed backfilling when folders arrived:
///
/// <list type="bullet">
///   <item><b>Archived</b> - put away by its owner. The one that <i>is</i> stored, because nothing else
///     about a thing could say it: being put away is a decision somebody makes rather than something it
///     becomes. See Note.Archive and its three counterparts.</item>
///   <item><b>Finished</b> - a task list with every entry ticked off. It goes there on its own, and comes
///     back out on its own the moment something on it is reopened; a note is never in it, having
///     nothing to finish.</item>
///   <item><b>Private</b> - an item sealed for its owner alone, filed nowhere else.</item>
///   <item><b>Public</b> - the ordinary one, and where anything lands that nobody filed anywhere.</item>
/// </list>
///
/// In that order, because the first that applies wins. <b>Archived beats even a folder somebody made</b>,
/// which is the whole point of it: putting something away is a decision about whether it is in front of
/// the reader at all, and an archived note still sitting under "Work" would not have been put anywhere.
/// A finished list gathers under Finished even when its owner filed it under a folder of their own,
/// since "what is still to do" is the question that folder is asked and a finished list would be an
/// answer nobody wanted. Deciding the rest from what an item already is means the folder can never
/// disagree with the item - there is no way to be filed as private while not being sealed.
///
/// Archiving leaves the folder id alone, so bringing something back puts it under the folder it was
/// under rather than somewhere a rule had to choose for it.
///
/// A folder somebody made is none of these, and holds whatever they put in it - private things
/// included, since filing something is not the same decision as sealing it.
/// </summary>
public enum BuiltInFolder
{
    Public,
    Private,
    Finished,

    /// <summary>Put away rather than deleted - added 2026-09-15, the user's decision of that date.</summary>
    Archived
}
