namespace Orbit.Core.Permissions;

/// <summary>
/// A part of Orbit that has to be unlocked before an account can use it at all. Each one gates a set of
/// endpoints (see PermissionPolicies in Orbit.Api) rather than a single feature flag in the client - the
/// client only hides what it already knows is refused.
/// </summary>
public enum ApplicationPermission
{
    /// <summary>Recording your own position, sharing it, and reading positions shared with you.</summary>
    Location,

    /// <summary>One-to-one conversations, including the contact list they are listed on.</summary>
    Chat,

    /// <summary>Group conversations - separate from <see cref="Chat"/>, so one can be granted without the other.</summary>
    GroupChat,

    /// <summary>Handing a note, task list, calendar event or warehouse to another Orbit account.</summary>
    Sharing
}
