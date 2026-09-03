using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Sharing;

namespace Orbit.Mobile.Api;

/// <summary>
/// The answer to asking to hold a shared item while it is being edited: either nobody else is in it,
/// or somebody is, and this says who.
/// </summary>
public sealed record EditClaim(string? HeldByOtherUserName)
{
    /// <summary>
    /// Also what a server that could not be reached comes back with. A lock nobody could ask about must
    /// not shut the reader out of their own editor - the offline policy already covers that case, and
    /// it does it from what the phone knows rather than from a failed request.
    /// </summary>
    public static readonly EditClaim Free = new((string?)null);

    public bool IsHeldByAnother => HeldByOtherUserName is not null;
}

/// <summary>
/// Notes, task lists, calendar events and inventories are all held the same way while somebody edits
/// them, so <see cref="Screens.EditLock"/> holds any of them through this rather than four times over.
/// Orbit.Web repeats the whole of it in each of its four editors.
/// </summary>
public interface ILockableItems
{
    Task<EditClaim> AcquireLockAsync(Guid serverId, CancellationToken cancellationToken = default);

    Task ReleaseLockAsync(Guid serverId, CancellationToken cancellationToken = default);
}

/// <summary>The shared half of every client's implementation - the two calls differ only in their path.</summary>
public static class EditLocking
{
    public static async Task<EditClaim> AcquireAsync(
        HttpClient httpClient, string path, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsync(path, content: null, cancellationToken);
        if (response.StatusCode is not HttpStatusCode.Conflict)
        {
            return EditClaim.Free;
        }

        var conflict = await response.Content.ReadFromJsonAsync<LockConflictDto>(cancellationToken);
        return new EditClaim(conflict?.LockedByUserName);
    }

    /// <summary>
    /// Best-effort. A lock left behind expires on its own within a minute, so a release that never
    /// arrives costs somebody a short wait rather than the item.
    /// </summary>
    public static async Task ReleaseAsync(HttpClient httpClient, string path, CancellationToken cancellationToken)
    {
        using var response = await httpClient.DeleteAsync(path, cancellationToken);
    }
}
