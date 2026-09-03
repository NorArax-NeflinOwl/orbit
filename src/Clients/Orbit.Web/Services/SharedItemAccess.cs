using Orbit.Core.Abstractions;

namespace Orbit.Web.Services;

/// <summary>
/// What the access level on a note, task list, event or inventory DTO lets this reader do. The rules
/// themselves are asked of <see cref="ShareAccess"/> rather than restated here: the client and the
/// server have to agree on them, and four editors each spelling them out in string comparisons is four
/// chances to disagree - which is exactly what happened when EditOnly was added and every
/// <c>!= "CanEdit"</c> quietly started calling an editor a read-only reader.
/// </summary>
/// <param name="IsShared">False for the reader's own item, which carries no restriction at all.</param>
public readonly record struct SharedItemAccess(bool IsShared, ShareAccessLevel Level)
{
    /// <summary>Unrecognised levels read as ReadOnly: an unknown level is one this client doesn't understand yet, and the safe reading of that is the narrowest one.</summary>
    public static SharedItemAccess For(bool isShared, string? accessLevel)
        => new(isShared, Enum.TryParse<ShareAccessLevel>(accessLevel, out var parsed) ? parsed : ShareAccessLevel.ReadOnly);

    public bool CanEdit => !IsShared || Level.AllowsEditing();

    /// <summary>Whether the sharing form is worth offering at all - it is, if they can hand out anything.</summary>
    public bool CanShare => !IsShared || Level.CanGrant(ShareAccessLevel.ReadOnly);

    public bool CanGrantEditOnly => !IsShared || Level.CanGrant(ShareAccessLevel.EditOnly);

    public bool CanGrantCanEdit => !IsShared || Level.CanGrant(ShareAccessLevel.CanEdit);

    /// <summary>Whether to offer asking the owner for more - only worth it for someone else's item they can't already change.</summary>
    public bool CanAskToEdit => IsShared && !Level.AllowsEditing();

    /// <summary>The half-sentence the shared-by banner ends with, so all four editors word it identically.</summary>
    public string Description => Level switch
    {
        ShareAccessLevel.CanEdit => "you can edit it",
        ShareAccessLevel.EditOnly => "you can edit it, but not share it further with editing",
        ShareAccessLevel.Share => "you can share it further, but not edit it",
        _ => "you can view it"
    };
}
