using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.JsonWebTokens;
using Orbit.Core.Abstractions;
using Orbit.Core.Users.SetPresence;

namespace Orbit.Api.LiveUpdates;

/// <summary>
/// The connection a signed-in client holds open so the server can tell it something changed, instead of
/// it asking four times a second.
///
/// Deliberately almost empty. Everything the server has to say goes out through
/// <see cref="LiveUpdateAnnouncer"/> as a bare announcement, and the client answers it by
/// fetching over the API it already uses - so nothing that was end-to-end encrypted takes a new route,
/// and no read path exists here that the HTTP endpoints do not already guard.
///
/// The one thing that comes *up* this connection is presence, and only because it is cheaper here: it
/// was an HTTP request every twenty seconds per open tab.
/// </summary>
[Authorize]
public sealed class LiveUpdatesHub(IDispatcher dispatcher, ILogger<LiveUpdatesHub> logger) : Hub
{
    /// <summary>
    /// Somebody arriving is somebody present, so the connection itself counts as being seen - it saves
    /// the client an immediate round trip to say what connecting already said.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        await RecordThatSomebodyIsHereAsync();
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Says somebody is still at the keyboard. <paramref name="isAtTheKeyboard"/> is false for a tab in
    /// the background, and a false is not simply "do not record" - it is the client declining to claim
    /// presence it does not have, which lets the account age to away and then offline exactly as it does
    /// today (see UserPresence.StatusAt). The connection staying open must not, on its own, keep
    /// somebody looking available behind thirty other tabs.
    /// </summary>
    public async Task ReportPresenceAsync(bool isAtTheKeyboard)
    {
        if (!isAtTheKeyboard)
        {
            return;
        }

        await RecordThatSomebodyIsHereAsync();
    }

    private async Task RecordThatSomebodyIsHereAsync()
    {
        if (UserId() is not { } userId)
        {
            return;
        }

        try
        {
            await dispatcher.SendAsync(new PresenceHeartbeatCommand(userId), Context.ConnectionAborted);
        }
        catch (OperationCanceledException)
        {
            // The connection went away mid-write. The silence is what the server should conclude anyway.
        }
        catch (Exception exception)
        {
            // A presence write that fails is a row that ages a little early, not a reason to drop a
            // connection carrying chat and notifications. Thrown out of a hub method it would do the latter.
            logger.LogWarning(exception, "Could not record presence for the connected account");
        }
    }

    private Guid? UserId()
        => Guid.TryParse(Context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId) ? userId : null;
}
