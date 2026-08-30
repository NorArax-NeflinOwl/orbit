using System.ClientModel;
using System.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using Orbit.Core.Assistant;

namespace Orbit.Api.Assistant;

/// <summary>
/// Talks to whichever model <see cref="AssistantSettings"/> points at, over the OpenAI-compatible chat
/// API that both of the assistant's two homes speak - Ollama on a developer machine and a hosted model
/// in Azure AI Foundry (see info/ai-assistant-plan.md). Named for what it does rather than for either
/// of them, because the whole point of the seam is that it does not know which one it reached.
///
/// Microsoft.Extensions.AI supplies the abstraction over the OpenAI client, and is what will carry tool
/// calling when the assistant gets tools. Semantic Kernel solves orchestration problems Orbit does not
/// have, so it is deliberately not here.
/// </summary>
public sealed class AssistantChatClient : IAssistantChatClient, IDisposable
{
    /// <summary>
    /// Ollama accepts any key and ignores it, but the OpenAI client refuses to be built without one.
    /// Not a credential - a placeholder that keeps an unauthenticated local endpoint reachable.
    /// </summary>
    private const string UnauthenticatedLocalModelKey = "no-key";

    private readonly ILogger<AssistantChatClient> _logger;
    private readonly AssistantSettings _settings;

    /// <summary>Null when no model is configured, which is the normal state of a fresh checkout.</summary>
    private readonly IChatClient? _chatClient;

    /// <summary>
    /// Reads its configuration once at startup rather than watching for changes, like JwtSettings and
    /// unlike the notification senders: the model connection is built from these values, and rebuilding
    /// it underneath an in-flight request would buy nothing on a setting that only ever changes on
    /// deployment.
    /// </summary>
    public AssistantChatClient(IOptions<AssistantSettings> settings, ILogger<AssistantChatClient> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        if (!_settings.IsConfigured)
        {
            _logger.LogInformation(
                "No assistant model is configured (see Assistant:Endpoint and Assistant:Model) - the assistant endpoint will say so");
            return;
        }

        var credential = new ApiKeyCredential(
            string.IsNullOrWhiteSpace(_settings.ApiKey) ? UnauthenticatedLocalModelKey : _settings.ApiKey);
        var openAiClient = new OpenAIClient(credential, new OpenAIClientOptions
        {
            Endpoint = new Uri(_settings.Endpoint),
            NetworkTimeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds)
        });

        _chatClient = openAiClient.GetChatClient(_settings.Model).AsIChatClient();
    }

    public bool IsConfigured => _settings.IsConfigured;

    /// <summary>
    /// Throws when no model is configured. Unlike a dropped email or push notification, there is no
    /// useful "skip it" here - the caller asked a question and is waiting for an answer, so the
    /// endpoint checks <see cref="IsConfigured"/> and tells the user, rather than letting this happen.
    /// </summary>
    public async Task<AssistantReply> AskAsync(string instructions, string question, CancellationToken cancellationToken)
    {
        if (_chatClient is null)
        {
            throw new InvalidOperationException(
                "No assistant model is configured - set Assistant:Endpoint and Assistant:Model, or check IsConfigured first.");
        }

        List<ChatMessage> messages =
        [
            new(ChatRole.System, instructions),
            new(ChatRole.User, question)
        ];

        var stopwatch = Stopwatch.StartNew();
        var response = await _chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
        stopwatch.Stop();

        // Logged because latency is the thing worth watching about this dependency: the same model
        // answered identical requests in 1 to 28 seconds while being measured (see
        // info/ai-assistant-local-model-measurements.md), and only the server sees the whole series.
        _logger.LogInformation(
            "Assistant model {Model} answered in {DurationMs} ms", _settings.Model, stopwatch.Elapsed.TotalMilliseconds);

        return new AssistantReply(response.Text, _settings.Model, stopwatch.Elapsed);
    }

    public void Dispose() => _chatClient?.Dispose();
}
