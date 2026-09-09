namespace Orbit.Core.Abstractions;

/// <summary>
/// The three answers a tick box gives, on a checklist entry and on a note's line alike: nothing yet,
/// done, or given up on. Stored as two flags rather than as this - see Orbit.Core.Tasks.TaskItem.IsFailed
/// for why - so this is what the clients read them as and cycle through, in one place so that pressing
/// a box means the same thing in a browser and on a phone.
/// </summary>
public enum TickState
{
    /// <summary>Nobody has answered yet. What every entry and every line starts as.</summary>
    None,

    /// <summary>Done.</summary>
    Completed,

    /// <summary>Finished with, and not done - the cross. See Orbit.Core.Tasks.TaskItem.IsFailed.</summary>
    Failed
}

public static class Ticks
{
    /// <summary>The two flags as they are stored, read as one answer. A tick wins, as it does everywhere else.</summary>
    public static TickState Read(bool isCompleted, bool isFailed)
        => isCompleted ? TickState.Completed : isFailed ? TickState.Failed : TickState.None;

    /// <summary>
    /// What one press does: nothing → done → given up on → nothing. One press rather than a gesture or
    /// a menu, because the everyday act is the first step and every step has to stay a single tap on a
    /// phone; the cost is that taking a tick back is two presses rather than one.
    /// </summary>
    public static TickState Next(this TickState state)
        => state switch
        {
            TickState.None => TickState.Completed,
            TickState.Completed => TickState.Failed,
            _ => TickState.None
        };

    public static bool IsCompleted(this TickState state) => state == TickState.Completed;

    public static bool IsFailed(this TickState state) => state == TickState.Failed;

    /// <summary>Finished with, either way - see Orbit.Core.Tasks.TaskItem.IsResolved, which is the same question.</summary>
    public static bool IsResolved(this TickState state) => state != TickState.None;
}
