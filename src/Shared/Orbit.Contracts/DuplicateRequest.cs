namespace Orbit.Contracts;

/// <summary>
/// Making a second one of something - a note, a task list, an inventory or a calendar event - with
/// everything that was on it. One request for all four, because it is one question: they differ in what
/// gets copied, and nothing about what the caller has to say.
/// </summary>
/// <param name="Name">
/// What to call the copy, or null to keep the original's name. The client names it, because the server
/// would have to write "(copy)" in a language it does not know the reader is using - see
/// Orbit.Core.OrbitWrittenNames for the other half of that problem.
///
/// Null is also the only possible answer for a sealed item: its real name is inside a payload the server
/// cannot open, so a copy of one is named exactly what the original was and nothing here can change that.
/// </param>
public sealed record DuplicateRequest(string? Name = null);
