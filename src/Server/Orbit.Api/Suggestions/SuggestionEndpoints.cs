using System.Security.Claims;
using Orbit.Contracts.Suggestions;
using Orbit.Core.Abstractions;
using Orbit.Core.Suggestions;
using Orbit.Core.Suggestions.GetNameSuggestions;

namespace Orbit.Api.Suggestions;

/// <summary>
/// Names the reader has already used, offered as they type one.
///
/// Behind ordinary authentication and nothing else: it reads only the caller's own rows, and it is the
/// one endpoint hit while somebody types, so a permission gate they could be missing would turn a text
/// field into a stream of refusals.
/// </summary>
public static class SuggestionEndpoints
{
    public static void MapSuggestionEndpoints(this IEndpointRouteBuilder app)
    {
        var suggestions = app.MapGroup("/api/suggestions").RequireAuthorization();

        suggestions.MapGet("/names", async (
            string kind, string? query, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var suggestionKind = RequestEnum.Parse<NameSuggestionKind>(kind, "kind");
            var found = await dispatcher.SendAsync(
                new GetNameSuggestionsQuery(GetUserId(user), suggestionKind, query ?? string.Empty), cancellationToken);

            return Results.Ok(found.Select(suggestion => new NameSuggestionDto(suggestion.Name, suggestion.Similarity)));
        });
    }

    private static Guid GetUserId(ClaimsPrincipal user)
        => Guid.Parse(user.FindFirstValue("sub")!);
}
