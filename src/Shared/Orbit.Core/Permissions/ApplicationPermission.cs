namespace Orbit.Core.Permissions;

/// <summary>
/// A part of Orbit that has to be unlocked before an account can use it at all. Each one gates a set of
/// endpoints (see PermissionPolicies in Orbit.Api) rather than a single feature flag in the client - the
/// client only hides what it already knows is refused.
///
/// <see cref="Contacts"/> is the one the others rest on, because it is the one about other people
/// existing at all - see PermissionPrerequisites.
/// </summary>
public enum ApplicationPermission
{
    /// <summary>
    /// Being able to find other accounts, and to be found by them. An account without this is invisible:
    /// it cannot look anybody up, and nobody's search turns it up either. Everything that involves a
    /// second person needs it first.
    /// </summary>
    Contacts,

    /// <summary>Conversations, with one other person or with several.</summary>
    Chat,

    /// <summary>Handing a note, task list, calendar event or warehouse to another Orbit account.</summary>
    Sharing,

    /// <summary>
    /// Recording where you are. Stands on its own - where somebody is has nothing to do with whether
    /// they can reach anyone - but sharing a position, or seeing one somebody shared, is about other
    /// people and needs <see cref="Contacts"/> as well.
    /// </summary>
    Location
}
