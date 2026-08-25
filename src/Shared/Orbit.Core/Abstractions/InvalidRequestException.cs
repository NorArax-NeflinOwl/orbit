namespace Orbit.Core.Abstractions;

/// <summary>
/// The one way anything in Orbit refuses what a caller asked for: the request was understood and is
/// simply not allowed - an event ending before it starts, a task list link that would close a cycle, a
/// field naming a value that doesn't exist. Orbit.Api answers every one of these with 400 and this
/// message (see InvalidRequestExceptionHandler), so the caller learns which rule it broke instead of
/// meeting an unexplained 500.
///
/// Deliberately distinct from a plain <see cref="ArgumentException"/>: that is also what the framework
/// throws when Orbit's own code passes something impossible, and those are faults that must keep
/// surfacing as 500s rather than being reported to the caller as their mistake. It still derives from
/// <see cref="ArgumentException"/> so existing catch clauses and tests that expect that type keep
/// working.
/// </summary>
public sealed class InvalidRequestException : ArgumentException
{
    public InvalidRequestException(string message) : base(message)
    {
    }
}
