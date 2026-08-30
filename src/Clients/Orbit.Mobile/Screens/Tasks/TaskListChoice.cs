using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Screens.Tasks;

/// <summary>
/// Another list an entry can be moved into, or pointed at. Only lists the server knows about: both acts
/// name a list by its real id, so a list that has never synced is not somewhere anything can go yet.
/// </summary>
/// <param name="ServerId">Null only for the "no list" choice a picker needs to offer.</param>
public sealed record TaskListChoice(Guid? ServerId, string Name)
{
    /// <summary>Pointing at nothing, which is what most entries do - see TaskItemEditor.ChosenLinkedTaskList.</summary>
    public static TaskListChoice NoList(Translations translations) => new(null, translations["None"]);
}
