using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Orbit.Contracts.Tags;
using Orbit.Core.Abstractions;
using Orbit.Core.Tags;
using Orbit.Core.Tags.GetTagColours;
using Orbit.Core.Tags.SetTagColour;

namespace Orbit.Api.Tags;

/// <summary>
/// The colours an account gives its tags - see Orbit.Core.Tags.TagColour. The tags themselves travel on
/// the notes and task lists carrying them; only their colours are an account setting with endpoints of
/// their own, because a colour belongs to the word rather than to any one item.
/// </summary>
public static class TagEndpoints
{
    public static void MapTagEndpoints(this WebApplication app)
    {
        var tags = app.MapGroup("/api/tags").RequireAuthorization();

        tags.MapGet("/colours", async (ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
            Results.Ok(ToDtos(await dispatcher.SendAsync(new GetTagColoursQuery(GetUserId(user)), cancellationToken))));

        // One tag at a time, answered with every colour afterwards: a client draws from what the server
        // now holds rather than from what it asked for. An empty colour takes the tag's colour away.
        tags.MapPut("/colours", async (
            SetTagColourRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
            Results.Ok(ToDtos(await dispatcher.SendAsync(
                new SetTagColourCommand(GetUserId(user), request.Tag, request.Colour), cancellationToken))));
    }

    private static IReadOnlyList<TagColourDto> ToDtos(IReadOnlyList<TagColour> colours)
        => [.. colours.Select(colour => new TagColourDto(colour.Tag, colour.Colour))];

    /// <summary>The group requires authorization, and Orbit.Api only ever issues tokens with this claim - see NotificationEndpoints.</summary>
    private static Guid GetUserId(ClaimsPrincipal user)
    {
        var subject = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("Authenticated request is missing a 'sub' claim.");
        return Guid.Parse(subject);
    }
}
