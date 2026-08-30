namespace Orbit.Core.Assistant;

/// <summary>
/// One turn with a language model: instructions and a question in, what the model said out.
///
/// Implemented outside Orbit.Core (see AssistantChatClient in Orbit.Api) so nothing in the domain
/// depends on a particular model, vendor or client library. Locally that model is Ollama in a container
/// (see docker-compose.yml); in production it is a small hosted model in Azure AI Foundry, and the
/// difference between the two is configuration rather than code - see info/ai-assistant-plan.md.
///
/// Deliberately one call with no memory of its own: the conversation, the context and eventually the
/// tools are the caller's to assemble, which is what keeps the privacy boundary in one reviewable place
/// (see the plan's "What the assistant is allowed to see"). Nothing here reaches the database.
/// </summary>
public interface IAssistantChatClient
{
    /// <summary>
    /// False until a model has been configured. Callers say so rather than failing - a fresh checkout
    /// runs with no assistant at all, the way it runs with no SMTP server and no VAPID key pair.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Asks the model one question under the given instructions.
    /// </summary>
    /// <param name="instructions">
    /// What the model is supposed to be and do - the system prompt. Separate from the question because
    /// it is written by Orbit and the question is not: everything a user typed stays on the untrusted
    /// side of that line.
    /// </param>
    Task<AssistantReply> AskAsync(string instructions, string question, CancellationToken cancellationToken);
}
