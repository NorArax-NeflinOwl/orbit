namespace Orbit.Api.Assistant;

/// <summary>
/// Which language model the assistant talks to, and how to reach it. Bound from the "Assistant"
/// configuration section; <see cref="ApiKey"/> must come from an environment variable or user-secrets,
/// never from a committed appsettings file (see .env.example).
///
/// One shape covers both homes the model has (see info/ai-assistant-plan.md): Ollama on a developer
/// machine, which needs no key, and a small hosted model in Azure AI Foundry, which does. Both expose
/// the same OpenAI-compatible API, so moving between them is these three values changing.
/// </summary>
public sealed class AssistantSettings
{
    public const string SectionName = "Assistant";

    /// <summary>
    /// Base address of an OpenAI-compatible endpoint, including the version segment - e.g.
    /// "http://ollama:11434/v1" for the container in docker-compose.yml.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Secret. Empty for a local Ollama, which authenticates nobody.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// The model to ask for - a pulled Ollama model such as "llama3.2:3b", or, on Azure AI Foundry,
    /// the name the deployment was given.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// How long to wait for a whole reply before giving up. Generous on purpose: a model running on a
    /// developer machine's CPU has no latency floor, and a request that eventually answers is worth
    /// more here than one cut off at a tidy number - see
    /// info/ai-assistant-local-model-measurements.md. A user-facing screen should impose its own,
    /// shorter patience on top of this.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// False until a model has been configured - the assistant endpoint then says so instead of
    /// failing, so a fresh checkout still runs with no model anywhere (see SmtpSettings.IsConfigured
    /// for the same reasoning applied to calendar event reminder emails). No check on
    /// <see cref="ApiKey"/>: a local model legitimately has none.
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Endpoint) && !string.IsNullOrWhiteSpace(Model);
}
