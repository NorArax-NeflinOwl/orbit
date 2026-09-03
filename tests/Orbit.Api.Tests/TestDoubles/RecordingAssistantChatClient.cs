using Orbit.Core.Assistant;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="IAssistantChatClient"/> stub that records what would have been asked instead of
/// reaching a model, so tests can assert on the request the endpoint built - and on a server where no
/// model is configured at all, by constructing it with <c>isConfigured: false</c>.
/// </summary>
internal sealed class RecordingAssistantChatClient : IAssistantChatClient
{
    private readonly List<AskedQuestion> _askedQuestions = [];
    private readonly string _answer;

    public RecordingAssistantChatClient(bool isConfigured = true, string answer = "An answer.")
    {
        IsConfigured = isConfigured;
        _answer = answer;
    }

    public bool IsConfigured { get; }

    public IReadOnlyList<AskedQuestion> AskedQuestions => _askedQuestions;

    public Task<AssistantReply> AskAsync(string instructions, string question, CancellationToken cancellationToken)
    {
        _askedQuestions.Add(new AskedQuestion(instructions, question));
        return Task.FromResult(new AssistantReply(_answer, "test-model", TimeSpan.FromMilliseconds(1234)));
    }

    internal sealed record AskedQuestion(string Instructions, string Question);
}
