using Orbit.Contracts.Users;
using Orbit.Core.Abstractions;
using Orbit.Core.Users;
using Orbit.Core.Users.Login;
using Orbit.Core.Users.RegisterUser;

namespace Orbit.Api.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var auth = app.MapGroup("/api/auth");

        auth.MapPost("/register", async (
            RegisterUserRequest request, IDispatcher dispatcher, TokenService tokenService, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(
                new RegisterUserCommand(request.Email, request.DisplayName, request.Password), cancellationToken);

            if (result.User is null)
            {
                return Results.Conflict(new { message = result.Error });
            }

            return Results.Ok(ToAuthResponse(result.User, tokenService));
        });

        auth.MapPost("/login", async (
            LoginRequest request, IDispatcher dispatcher, TokenService tokenService, CancellationToken cancellationToken) =>
        {
            var user = await dispatcher.SendAsync(new LoginQuery(request.Email, request.Password), cancellationToken);

            return user is null ? Results.Unauthorized() : Results.Ok(ToAuthResponse(user, tokenService));
        });
    }

    private static AuthResponse ToAuthResponse(User user, TokenService tokenService)
        => new(tokenService.CreateToken(user), user.Id, user.Email, user.DisplayName);
}
