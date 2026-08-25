using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Orbit.Contracts.Users;
using Orbit.Core.Abstractions;
using Orbit.Core.Users;
using Orbit.Core.Users.SaveOwnLocation;
using Orbit.Core.Users.GetUserById;
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

        // The signed-in account itself: everything under /me is scoped to the caller's own token, never
        // to an id in the route, so one account can never read or edit another's profile.
        users.MapGet("/me", async (ClaimsPrincipal user, IUserRepository userRepository, CancellationToken cancellationToken) =>
        {
            var account = await userRepository.GetByIdAsync(GetUserId(user), cancellationToken);
            return account is null
                ? Results.NotFound()
                : Results.Ok(new AccountDto(
                    account.Id, account.Email, account.UserName, account.DisplayName, account.IsEmailVerified,
                    account.HasPassword, account.GoogleSubjectId is not null, ToDto(account.Location)));
        });


        // Recording a location is always the user's own doing - there is no endpoint for writing anyone
        // else's, and none for reading one either: the account's own /me above is the only way out.
        users.MapPut("/me/location", async (
            SaveOwnLocationRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var location = new UserLocation(request.Address, request.Latitude, request.Longitude, DateTimeOffset.UtcNow);
            var saved = await dispatcher.SendAsync(new SaveOwnLocationCommand(GetUserId(user), location), cancellationToken);
            return saved ? Results.NoContent() : Results.NotFound();
        });

        users.MapDelete("/me/location", async (ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var cleared = await dispatcher.SendAsync(new SaveOwnLocationCommand(GetUserId(user), Location: null), cancellationToken);
            return cleared ? Results.NoContent() : Results.NotFound();
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
            var confirmed = await dispatcher.SendAsync(
                new ConfirmEmailVerificationCommand(GetUserId(user), request.Code), cancellationToken);
            return confirmed ? Results.NoContent() : Results.BadRequest(new { message = "That code isn't valid any more. Request a new one." });
        }).RequireRateLimiting(RateLimiterPolicyNames.Auth);

        users.MapGet("/search", async (
            string query, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new SearchUserQuery(GetUserId(user), query), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(ToDto(result));
        });

        users.MapGet("/{id:guid}", async (Guid id, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new GetUserByIdQuery(id), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(ToDto(result));
        });

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
