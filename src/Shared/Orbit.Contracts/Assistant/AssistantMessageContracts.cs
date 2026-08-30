namespace Orbit.Contracts.Assistant;

/// <summary>
/// A single question for the assistant. No conversation id and no screen context yet - this is the
/// round trip on its own (see info/ai-assistant-plan.md's build order), and the context assembly that
/// carries the caller's own data comes as its own step, with its own privacy boundary to review.
/// </summary>
public sealed record AssistantMessageRequest(string Question);

/// <summary>
/// The model's answer, plus what it cost to get it.
/// </summary>
/// <param name="Answer">What the model said. Text to show a person, never something to act on.</param>
/// <param name="Model">Which model answered - locally whatever was pulled into Ollama.</param>
/// <param name="DurationMilliseconds">
/// How long the model itself took, so the reply's cost is visible to whoever is measuring it rather
/// than only in the server's logs.
/// </param>
public sealed record AssistantMessageResponse(string Answer, string Model, double DurationMilliseconds);
