using Orbit.Contracts;
using Orbit.Contracts.Assistant;
using Orbit.Core.Assistant;

namespace Orbit.Api.Assistant;

public static class AssistantEndpoints
{
    /// <summary>
    /// The fixed system prompt for this first round trip. It says only what the assistant is, because
    /// there is nothing else it can honestly say yet: no context is assembled, no tools exist, and the
    /// model is told none of the user's data. The capability summary that replaces this - what Orbit
    /// can do, what sharing means - is its own step in info/ai-assistant-plan.md.
    /// </summary>
    private const string Instructions =
        "You are Orbit's assistant. Orbit is a household organiser: inventories of inventory items, task " +
        "lists, a calendar and sharing between people. Answer briefly, in the language the question was " +
        "asked in. You have not been given any of this user's data, so if a question needs it, say that " +
        "you cannot see it yet rather than inventing an answer.";

    /// <summary>
    /// The longest question the endpoint will forward. A ceiling rather than a considered limit: the
    /// endpoint hands arbitrary text to a model that is billed per token in production, and a request
    /// nobody can send is cheaper to refuse than to answer - see the plan's "What this costs".
    /// </summary>
    private const int MaximumQuestionLength = 2000;

    /// <summary>
    /// POST /api/assistant/messages - one question, one answer, nothing remembered between them.
    ///
    /// Authenticated because the model costs money per request in production, and rate-limited under the
    /// same policy as the auth endpoints, which is the strictest one configured.
    /// </summary>
    public static void MapAssistantEndpoints(this WebApplication app)
    {
        var assistant = app.MapGroup("/api/assistant").RequireAuthorization();

        assistant.MapPost("/messages", AskAsync)
            .RequireRateLimiting(RateLimiterPolicyNames.Auth);
    }

    // Internal rather than private so Orbit.Api.Tests can call the handler directly (see the
    // InternalsVisibleTo entry in Orbit.Api.csproj) without standing up a model.
    internal static async Task<IResult> AskAsync(
        AssistantMessageRequest request, IAssistantChatClient chatClient, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return Results.BadRequest(new RefusalDto("Ask the assistant something."));
        }

        if (request.Question.Length > MaximumQuestionLength)
        {
            return Results.BadRequest(
                new RefusalDto($"A question can be at most {MaximumQuestionLength} characters long."));
        }

        if (!chatClient.IsConfigured)
        {
            // Not an error in the deployment's own terms - an Orbit without a model is a supported way
            // to run it - so this says what is missing instead of pretending something broke.
            return Results.Json(
                new RefusalDto("The assistant is not configured on this server."),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var reply = await chatClient.AskAsync(Instructions, request.Question, cancellationToken);

        return Results.Ok(new AssistantMessageResponse(reply.Text, reply.Model, reply.Duration.TotalMilliseconds));
    }
}
