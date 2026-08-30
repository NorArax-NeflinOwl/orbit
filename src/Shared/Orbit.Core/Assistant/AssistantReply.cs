namespace Orbit.Core.Assistant;

/// <summary>
/// What the model said, which model said it, and how long it took.
///
/// The duration is part of the answer rather than only a log line because latency is the open question
/// at this stage of the assistant: a CPU-hosted model answered identical requests in anything from 1 to
/// 28 seconds on a developer machine (see info/ai-assistant-local-model-measurements.md), and a caller
/// deciding whether the feature is usable needs to see that rather than infer it.
/// </summary>
/// <param name="Text">The model's reply, as it came back - unparsed and untrusted.</param>
/// <param name="Model">Which model produced it, so a measurement can be attributed.</param>
/// <param name="Duration">Wall clock from sending the request to holding the whole reply.</param>
public sealed record AssistantReply(string Text, string Model, TimeSpan Duration);
