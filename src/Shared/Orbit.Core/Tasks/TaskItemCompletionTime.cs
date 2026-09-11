namespace Orbit.Core.Tasks;

/// <summary>
/// When an entry was done, as a client works it out at the moment of a tick - see
/// <see cref="TaskItem.CompletedAtUtc"/>. Both clients record it themselves rather than leaving it to the
/// server: a private list's entries are sealed and never reach the server, a phone ticks offline, and in
/// both cases the time has to be on the entry before anything is sent. The server applies the same rule
/// to a save that arrives without one (<see cref="TaskItem.RecordWhenItWasDone"/>).
/// </summary>
public static class TaskItemCompletionTime
{
    /// <summary>
    /// The time an entry carries once its box says <paramref name="isCompleted"/>: none for one that is
    /// not done, the one it already had for one that was already done, and <paramref name="nowUtc"/> for
    /// one that has only just been ticked. A cross is not a completion and so carries none either.
    /// </summary>
    public static DateTimeOffset? After(
        bool wasCompleted, DateTimeOffset? recordedUtc, bool isCompleted, DateTimeOffset nowUtc)
        => !isCompleted ? null : wasCompleted ? recordedUtc : nowUtc;
}
