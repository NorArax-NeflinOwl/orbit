using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Orbit.Api.Permissions;
using Orbit.Core.Permissions.RedeemPermissionCode;
using Orbit.Core.Permissions.GetUserPermissions;
using Orbit.Contracts.Users;
using Orbit.Core.Abstractions;
using Orbit.Core.Users;
using Orbit.Core.Users.SetPresence;
using Orbit.Core.Users.SaveOwnLocation;
using Orbit.Core.Location.GetSharedLocations;
using Orbit.Core.Location.StopReceivingLocation;
using Orbit.Core.Location.StopSharingLocation;
using Orbit.Core.Location.ShareLocation;
using Orbit.Core.Location;
using Orbit.Core.Users.GetUserById;
using Orbit.Core.Users.GetUsersByIds;
using Orbit.Core.Users.GetWrappedPrivateKey;
using Orbit.Core.Users.SearchUser;
using Orbit.Core.Users.ChangePassword;
using Orbit.Core.Users.DeleteAccount;
using Orbit.Core.Users.ConfirmEmailVerification;
using Orbit.Core.Users.RequestEmailVerification;
using Orbit.Core.Users.UpdateProfile;
using Orbit.Core.Users.LinkGoogleAccount;
using Orbit.Core.Users.SetPassword;
using Orbit.Core.Users.SetEncryptionKey;
using Orbit.Core.Users.SetPublicKey;

namespace Orbit.Api.Users;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        // Looking up other users' profiles and publishing your own public key both require knowing who
        // is asking, so the whole group requires a valid, authenticated caller.
        var users = app.MapGroup("/api/users").RequireAuthorization();

        // Recording where you are needs nothing but the permission for it: a position with nobody to
        // send it to is still worth having on your own map.
        var location = users.MapGroup("/me/location").RequireAuthorization(PermissionPolicies.Location);

        // Sharing one, and seeing one somebody shared, are about other people - so they need Contacts
        // as well. Both policies apply, and both have to pass.
        var locationSharing = users.MapGroup("/me/location")
            .RequireAuthorization(PermissionPolicies.Location, PermissionPolicies.Contacts);

        // The signed-in account itself: everything under /me is scoped to the caller's own token, never
        // to an id in the route, so one account can never read or edit another's profile.
        users.MapGet("/me", async (ClaimsPrincipal user, IUserRepository userRepository, CancellationToken cancellationToken) =>
        {
            var account = await userRepository.GetByIdAsync(GetUserId(user), cancellationToken);
            return account is null
                ? Results.NotFound()
                : Results.Ok(new AccountDto(
                    account.Id, account.Email, account.UserName, account.DisplayName, account.IsEmailVerified,
                    account.HasPassword, account.GoogleSubjectId is not null, ToDto(account.Location),
                    account.Presence.Availability.ToString(),
                    account.Presence.StatusAt(DateTimeOffset.UtcNow).ToString()));
        });

        // What this account may use. Always readable, and always about the caller alone - a page that
        // cannot ask what it is allowed to do can only find out by trying and being refused.
        users.MapGet("/me/permissions", async (ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var granted = await dispatcher.SendAsync(new GetUserPermissionsQuery(GetUserId(user)), cancellationToken);
            return Results.Ok(new UserPermissionsDto([.. granted.Select(permission => permission.ToString())]));
        });

        // Typing a code is the only way to gain a permission. Rate-limited like the other endpoints that
        // change an account, so the twelve characters cannot be worked out by trying them all.
        users.MapPost("/me/permissions/redeem", async (
            RedeemPermissionCodeRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var outcome = await dispatcher.SendAsync(
                new RedeemPermissionCodeCommand(GetUserId(user), request.Code), cancellationToken);
            return Results.Ok(new RedeemPermissionCodeResultDto(
                outcome.Granted?.ToString(), outcome.MissingPrerequisite?.ToString()));
        }).RequireRateLimiting(RateLimiterPolicyNames.Auth);

        // What the caller chose to be. Only their own: presence describes whether somebody is there to
        // answer, which nobody else is in a position to say for them.
        users.MapPut("/me/presence", async (
            SetAvailabilityRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var updated = await dispatcher.SendAsync(
                new SetAvailabilityCommand(GetUserId(user), RequestEnum.Parse<PresenceAvailability>(request.Availability, "availability")),
                cancellationToken);
            return updated ? Results.NoContent() : Results.NotFound();
        });

        // Sent by an open client every so often. The gap since the last one is what turns a green dot
        // yellow and then grey, so a client that stops sending fades out on its own - see UserPresence.
        users.MapPost("/me/presence/heartbeat", async (
            ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var recorded = await dispatcher.SendAsync(new PresenceHeartbeatCommand(GetUserId(user)), cancellationToken);
            return recorded ? Results.NoContent() : Results.NotFound();
        });


        // Recording a location is always the user's own doing - there is no endpoint for writing anyone
        // else's, and none for reading one either: the account's own /me above is the only way out.
        location.MapPut("/", async (
            SaveOwnLocationRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var location = new UserLocation(request.Address, request.Latitude, request.Longitude, DateTimeOffset.UtcNow);
            var saved = await dispatcher.SendAsync(new SaveOwnLocationCommand(GetUserId(user), location), cancellationToken);
            return saved ? Results.NoContent() : Results.NotFound();
        });

        location.MapDelete("/", async (ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var cleared = await dispatcher.SendAsync(new SaveOwnLocationCommand(GetUserId(user), Location: null), cancellationToken);
            return cleared ? Results.NoContent() : Results.NotFound();
        });


        // Sharing a position with one contact. Everything here is the caller's own doing: they share,
        // they refresh, they stop - and stopping deletes the row rather than flagging it, so a position
        // nobody is sharing any more is a position the database no longer holds.
        locationSharing.MapPut("/shares", async (
            ShareLocationRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            await dispatcher.SendAsync(
                new ShareLocationCommand(
                    GetUserId(user), request.RecipientUserId, request.CiphertextBase64, request.NonceBase64, request.IsContinuous),
                cancellationToken);
            return Results.NoContent();
        });

        locationSharing.MapDelete("/shares/{recipientUserId:guid}", async (
            Guid recipientUserId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            await dispatcher.SendAsync(new StopSharingLocationCommand(GetUserId(user), recipientUserId), cancellationToken);
            return Results.NoContent();
        });

        locationSharing.MapDelete("/shares", async (ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            await dispatcher.SendAsync(new StopSharingLocationCommand(GetUserId(user), RecipientUserId: null), cancellationToken);
            return Results.NoContent();
        });

        // The same row, ended from the other side. A share is an arrangement between two people, and
        // only the sharer could end it - which left a recipient with somebody's live position on their
        // map and nothing to do about it but ask. Deletes rather than hides: the position is gone.
        locationSharing.MapDelete("/shared-with-me/{sharerUserId:guid}", async (
            Guid sharerUserId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            await dispatcher.SendAsync(
                new StopReceivingLocationCommand(GetUserId(user), sharerUserId), cancellationToken);
            return Results.NoContent();
        });

        // Who the caller is currently sharing with, so they can see it and end any of it.
        locationSharing.MapGet("/shares", async (ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var shares = await dispatcher.SendAsync(new GetOwnLocationSharesQuery(GetUserId(user)), cancellationToken);
            return Results.Ok(shares.Select(ToDto));
        });

        // What other people are sharing with the caller - the endpoint a recipient polls for the latest
        // point. Returns ciphertext; only the recipient's own browser can open it.
        locationSharing.MapGet("/shared-with-me", async (
            ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var shares = await dispatcher.SendAsync(new GetSharedLocationsQuery(GetUserId(user)), cancellationToken);
            return Results.Ok(shares.Select(ToDto));
        });

        users.MapPut("/me/profile", async (
            UpdateProfileRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(
                new UpdateProfileCommand(GetUserId(user), request.DisplayName, request.UserName), cancellationToken);
            return result switch
            {
                UpdateProfileResult.Success => Results.NoContent(),
                UpdateProfileResult.UserNameTaken => Results.Conflict(new { message = "This username is already taken." }),
                _ => Results.NotFound()
            };
        });

        users.MapPut("/me/password", async (
            ChangePasswordRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var changed = await dispatcher.SendAsync(
                new ChangePasswordCommand(GetUserId(user), request.CurrentPassword, request.NewPassword), cancellationToken);
            // Unauthorized rather than NotFound: the only realistic failure here is a wrong current
            // password, and the caller is already authenticated.
            return changed ? Results.NoContent() : Results.Unauthorized();
        }).RequireRateLimiting(RateLimiterPolicyNames.Auth);

        users.MapDelete("/me", async (
            [FromBody] DeleteAccountRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var deleted = await dispatcher.SendAsync(new DeleteAccountCommand(GetUserId(user), request.Password), cancellationToken);
            // Unauthorized rather than NotFound: the only realistic failure here is a wrong password,
            // and the caller is already authenticated - mirrors /me/password just above.
            return deleted ? Results.NoContent() : Results.Unauthorized();
        }).RequireRateLimiting(RateLimiterPolicyNames.Auth);

        // The first password on an account that has none - a Google account reaching chat. Separate from
        // the change endpoint because there is no current password to prove.
        users.MapPost("/me/password", async (
            SetPasswordRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var set = await dispatcher.SendAsync(new SetPasswordCommand(GetUserId(user), request.NewPassword), cancellationToken);
            return set
                ? Results.NoContent()
                : Results.Conflict(new { message = "This account already has a password - change it instead." });
        }).RequireRateLimiting(RateLimiterPolicyNames.Auth);

        users.MapPost("/me/google", async (
            GoogleSignInRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new LinkGoogleAccountCommand(GetUserId(user), request.IdToken), cancellationToken);
            return ToLinkResult(result);
        }).RequireRateLimiting(RateLimiterPolicyNames.Auth);

        users.MapDelete("/me/google", async (ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new UnlinkGoogleAccountCommand(GetUserId(user)), cancellationToken);
            return ToLinkResult(result);
        });

        users.MapPost("/me/email-verification", async (
            RequestEmailVerificationRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(
                new RequestEmailVerificationCommand(GetUserId(user), request.EmailAddress), cancellationToken);
            return result switch
            {
                EmailVerificationRequestResult.Sent => Results.NoContent(),
                EmailVerificationRequestResult.EmailTaken => Results.Conflict(new { message = "An account with this email address already exists." }),
                _ => Results.NotFound()
            };
        }).RequireRateLimiting(RateLimiterPolicyNames.Auth);

        users.MapPost("/me/email-verification/confirm", async (
            ConfirmEmailVerificationRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(
                new ConfirmEmailVerificationCommand(GetUserId(user), request.Code), cancellationToken);
            return result switch
            {
                EmailVerificationConfirmResult.Confirmed => Results.NoContent(),
                EmailVerificationConfirmResult.EmailTaken => Results.Conflict(new { message = "An account with this email address already exists." }),
                _ => Results.BadRequest(new { message = "That code isn't valid any more. Request a new one." })
            };
        }).RequireRateLimiting(RateLimiterPolicyNames.Auth);

        users.MapGet("/search", async (
            string query, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new SearchUserQuery(GetUserId(user), query), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(ToDto(result));
        }).RequireAuthorization(PermissionPolicies.Contacts);

        // Several profiles in one request, for a caller that needs a whole roster - see
        // GetUsersByIdsQueryHandler. Ids repeat rather than being comma-separated
        // ("?ids=<a>&ids=<b>"), which is what minimal APIs bind an array from; a comma-separated list
        // is refused as a malformed query, the same as any other unparseable parameter.
        users.MapGet("/", async (
            [FromQuery(Name = "ids")] Guid[] ids, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var results = await dispatcher.SendAsync(new GetUsersByIdsQuery(ids), cancellationToken);
            return Results.Ok(results.Select(ToDto).ToList());
        }).RequireAuthorization(PermissionPolicies.Contacts);

        users.MapGet("/{id:guid}", async (Guid id, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new GetUserByIdQuery(id), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(ToDto(result));
        }).RequireAuthorization(PermissionPolicies.Contacts);

        users.MapPut("/me/public-key", async (
            SetPublicKeyRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            await dispatcher.SendAsync(new SetPublicKeyCommand(GetUserId(user), request.PublicKeyBase64), cancellationToken);
            return Results.NoContent();
        });

        users.MapPut("/me/encryption-key", async (
            SetEncryptionKeyRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var wrappedPrivateKey = new WrappedPrivateKey(
                request.WrappedPrivateKey.CiphertextBase64, request.WrappedPrivateKey.NonceBase64,
                request.WrappedPrivateKey.SaltBase64, request.WrappedPrivateKey.Iterations);
            await dispatcher.SendAsync(
                new SetEncryptionKeyCommand(GetUserId(user), request.PublicKeyBase64, wrappedPrivateKey), cancellationToken);
            return Results.NoContent();
        });

        // Null means the caller has never backed up a private key from this account (or only ever used a
        // browser predating this feature) - a normal state, not an error, so this is 204 rather than 404.
        users.MapGet("/me/encryption-key", async (
            ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new GetWrappedPrivateKeyQuery(GetUserId(user)), cancellationToken);
            return result is null ? Results.NoContent() : Results.Ok(ToDto(result));
        });
    }

    /// <summary>
    /// Reads the authenticated user's id out of the JWT's "sub" claim. Safe to assume it's present and
    /// valid: the group requires authorization, and Orbit.Api only ever issues tokens with this claim
    /// (see TokenService).
    /// </summary>
    private static IResult ToLinkResult(LinkGoogleAccountResult result) => result switch
    {
        LinkGoogleAccountResult.Success => Results.NoContent(),
        LinkGoogleAccountResult.InvalidToken => Results.Unauthorized(),
        LinkGoogleAccountResult.AlreadyLinkedElsewhere =>
            Results.Conflict(new { message = "That Google account is already connected to a different Orbit account." }),
        LinkGoogleAccountResult.WouldLockAccountOut =>
            Results.Conflict(new { message = "Set a password first - otherwise you'd have no way to sign in." }),
        _ => Results.NotFound()
    };



    private static SharedLocationDto ToDto(SharedLocation sharedLocation)
        => new(
            sharedLocation.SharerUserId, sharedLocation.RecipientUserId, sharedLocation.CiphertextBase64,
            sharedLocation.NonceBase64, sharedLocation.IsContinuous, sharedLocation.UpdatedAtUtc);

    private static UserLocationDto? ToDto(UserLocation? location)
        => location is null ? null : new UserLocationDto(location.Address, location.Latitude, location.Longitude, location.RecordedAtUtc);

    private static Guid GetUserId(ClaimsPrincipal user)
    {
        var subject = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("Authenticated request is missing a 'sub' claim.");
        return Guid.Parse(subject);
    }

    private static UserSearchResultDto ToDto(User user)
        => new(user.Id, user.UserName, user.DisplayName, user.PublicKeyBase64);

    private static WrappedPrivateKeyDto ToDto(WrappedPrivateKey wrappedPrivateKey)
        => new(
            wrappedPrivateKey.CiphertextBase64, wrappedPrivateKey.NonceBase64, wrappedPrivateKey.SaltBase64,
            wrappedPrivateKey.Iterations);
}
