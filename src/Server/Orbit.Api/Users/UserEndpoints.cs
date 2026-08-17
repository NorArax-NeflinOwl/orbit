using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Orbit.Contracts.Users;
using Orbit.Core.Abstractions;
using Orbit.Core.Users;
using Orbit.Core.Users.GetUserById;
using Orbit.Core.Users.SearchUser;
using Orbit.Core.Users.SetPublicKey;

namespace Orbit.Api.Users;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        // Looking up other users' profiles and publishing your own public key both require knowing who
        // is asking, so the whole group requires a valid, authenticated caller.
        var users = app.MapGroup("/api/users").RequireAuthorization();

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
    }

    /// <summary>
    /// Reads the authenticated user's id out of the JWT's "sub" claim. Safe to assume it's present and
    /// valid: the group requires authorization, and Orbit.Api only ever issues tokens with this claim
    /// (see TokenService).
    /// </summary>
    private static Guid GetUserId(ClaimsPrincipal user)
    {
        var subject = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("Authenticated request is missing a 'sub' claim.");
        return Guid.Parse(subject);
    }

    private static UserSearchResultDto ToDto(User user)
        => new(user.Id, user.UserName, user.DisplayName, user.PublicKeyBase64);
}
