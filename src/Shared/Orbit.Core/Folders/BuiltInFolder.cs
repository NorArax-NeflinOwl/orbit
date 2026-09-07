namespace Orbit.Core.Folders;

/// <summary>
/// The three folders every account has without having made them: everything is in one of them until it
/// is put somewhere else. They are not rows - which one something is in is decided from what it already
/// is, so nothing is stored and nothing needed backfilling when folders arrived:
///
/// <list type="bullet">
///   <item><b>Finished</b> - a task list with every entry ticked off. It goes there on its own, and comes
///     back out on its own the moment something on it is reopened; a note is never in it, having
///     nothing to finish.</item>
///   <item><b>Private</b> - an item sealed for its owner alone, filed nowhere else.</item>
///   <item><b>Public</b> - the ordinary one, and where anything lands that nobody filed anywhere.</item>
/// </list>
///
/// In that order, because the first that applies wins: a finished list gathers there even when its owner
/// filed it under a folder of their own, since "what is still to do" is the question that folder is
/// asked, and a finished list would be an answer nobody wanted. Deciding it from what an item already
/// is also means the folder can never disagree with the item - there is no way to be filed as private
/// while not being sealed, or to be gathered as finished with work left on it.
///
/// A folder somebody made is none of these, and holds whatever they put in it - private things
/// included, since filing something is not the same decision as sealing it.
/// </summary>
public enum BuiltInFolder
{
    Public,
    Private,
    Finished
}
