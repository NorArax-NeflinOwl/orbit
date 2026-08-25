using Orbit.Api;
using Orbit.Contracts.Users;
using Orbit.Core.Abstractions;
using Orbit.Core.Users;
using Orbit.Core.Users.Login;
using Orbit.Core.Users.RegisterUser;
using Orbit.Core.Users.RequestPasswordReset;
using Orbit.Core.Users.SignInWithGoogle;
using Orbit.Core.Users.ResetPassword;

namespace Orbit.Api.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var auth = app.MapGroup("/api/auth");

        // Only the two endpoints that accept a guessable credential (a password) share this brute-force
        // budget - see RequireRateLimiting below. /refresh and /logout are gated by possession of an
        // unguessable, high-entropy refresh token instead, so they don't need the same throttling, and
        // sharing it with them would let unrelated refresh/logout traffic (e.g. a session repeatedly
        // retrying a stale token in the background) exhaust the budget and lock out a legitimate login
        // attempt from the same IP.
        auth.MapPost("/register", async (
            RegisterUserRequest request, IDispatcher dispatcher, TokenService tokenService,
            RefreshTokenService refreshTokenService, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(
                new RegisterUserCommand(request.Email, request.UserName, request.DisplayName, request.Password), cancellationToken);

            if (result.User is null)
            {
                return Results.Conflict(new { message = result.Error });
            }

            return Results.Ok(await ToAuthResponseAsync(result.User, tokenService, refreshTokenService, cancellationToken));
        }).RequireRateLimiting(RateLimiterPolicyNames.Auth);

        auth.MapPost("/login", async (
            LoginRequest request, IDispatcher dispatcher, TokenService tokenService,
            RefreshTokenService refreshTokenService, CancellationToken cancellationToken) =>
        {
            var user = await dispatcher.SendAsync(new LoginQuery(request.EmailOrUserName, request.Password), cancellationToken);

            return user is null
                ? Results.Unauthorized()
                : Results.Ok(await ToAuthResponseAsync(user, tokenService, refreshTokenService, cancellationToken));
        }).RequireRateLimiting(RateLimiterPolicyNames.Auth);

        // Registration and login in one gesture: the browser has already obtained a Google ID token, and
        // whether that identity is new to Orbit is the server's business, not something the user picks.
        auth.MapPost("/google", async (
            GoogleSignInRequest request, IDispatcher dispatcher, TokenService tokenService,
            RefreshTokenService refreshTokenService, CancellationToken cancellationToken) =>
        {
            var user = await dispatcher.SendAsync(new SignInWithGoogleCommand(request.IdToken), cancellationToken);
            return user is null
                ? Results.Unauthorized()
                : Results.Ok(await ToAuthResponseAsync(user, tokenService, refreshTokenService, cancellationToken));
        }).RequireRateLimiting(RateLimiterPolicyNames.Auth);

        // Anonymous by necessity - the whole point is that the caller can't sign in. Rate-limited with the
        // same budget as login: both take a user-supplied identifier and a guessable secret.
        auth.MapPost("/password-reset", async (
            RequestPasswordResetRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            await dispatcher.SendAsync(new RequestPasswordResetCommand(request.EmailOrUserName), cancellationToken);
            // Always NoContent, whether or not that account exists or has a verified address - see
            // RequestPasswordResetCommand for why this endpoint must not be an account-existence oracle.
            return Results.NoContent();
        }).RequireRateLimiting(RateLimiterPolicyNames.Auth);

        auth.MapPost("/password-reset/confirm", async (
            ResetPasswordRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var reset = await dispatcher.SendAsync(
                new ResetPasswordCommand(request.EmailOrUserName, request.Code, request.NewPassword), cancellationToken);
            return reset ? Results.NoContent() : Results.BadRequest(new { message = "That code isn't valid any more. Request a new one." });
        }).RequireRateLimiting(RateLimiterPolicyNames.Auth);

        auth.MapPost("/refresh", async (
            RefreshTokenRequest request, IUserRepository userRepository, TokenService tokenService,
            RefreshTokenService refreshTokenService, CancellationToken cancellationToken) =>
        {
            var redemption = await refreshTokenService.RedeemAsync(request.RefreshToken, cancellationToken);
            if (redemption is null)
            {
                return Results.Unauthorized();
            }

            var user = await userRepository.GetByIdAsync(redemption.UserId, cancellationToken);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(ToAuthResponse(user, redemption.RefreshToken, tokenService));
        });

        auth.MapPost("/logout", async (
            RefreshTokenRequest request, RefreshTokenService refreshTokenService, CancellationToken cancellationToken) =>
        {
            await refreshTokenService.RevokeAsync(request.RefreshToken, cancellationToken);
            return Results.NoContent();
        });
    }

    private static async Task<AuthResponse> ToAuthResponseAsync(
        User user, TokenService tokenService, RefreshTokenService refreshTokenService, CancellationToken cancellationToken)
    {
        var refreshToken = await refreshTokenService.IssueAsync(user.Id, cancellationToken);
        return ToAuthResponse(user, refreshToken, tokenService);
    }

    private static AuthResponse ToAuthResponse(User user, string refreshToken, TokenService tokenService)
        => new(tokenService.CreateToken(user), refreshToken, user.Id, user.Email, user.DisplayName);
}
