using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Orbit.Api.LiveUpdates;

/// <summary>
/// Tells SignalR which account a connection belongs to, so announcing to one person reaches every tab
/// and device they have open.
///
/// Needed because the two halves disagree by default. SignalR looks for ClaimTypes.NameIdentifier;
/// Orbit's tokens carry the account in "sub" and deliberately keep it under that name - see Program.cs's
/// MapInboundClaims = false, and every endpoint's GetUserId, which reads the same claim. Without this,
/// Clients.User(...) matches nobody and no announcement is ever delivered - silently, because sending to
/// an account nobody is connected under is a perfectly ordinary thing to do.
/// </summary>
public sealed class SubjectClaimUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) => UserIdFrom(connection.User);

    /// <summary>
    /// Split out from the interface method so the claim it reads can be tested without standing up a
    /// connection - the choice of claim is the entire risk here, and it is one that fails silently.
    /// </summary>
    internal static string? UserIdFrom(ClaimsPrincipal? user)
        => user?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
}
