using System.Security.Claims;
using Orbit.Contracts.Suggestions;
using Orbit.Core.Abstractions;
using Orbit.Core.Suggestions;
using Orbit.Core.Suggestions.GetNameSuggestions;
using Orbit.Core.Suggestions.GetUsedValues;

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

        // The whole of what this reader has filed things under, rather than what looks like what they
        // are typing - see UsedValueKind for why the two are different questions. Asked once when an
        // editor opens, so there is no query to narrow it by.
        suggestions.MapGet("/used-values", async (
            string kind, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var valueKind = RequestEnum.Parse<UsedValueKind>(kind, "kind");
            var used = await dispatcher.SendAsync(new GetUsedValuesQuery(GetUserId(user), valueKind), cancellationToken);
            return Results.Ok(used);
        });
    }

    private static Guid GetUserId(ClaimsPrincipal user)
        => Guid.Parse(user.FindFirstValue("sub")!);
}
