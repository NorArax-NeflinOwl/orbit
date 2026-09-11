namespace Orbit.Core.Notes;

/// <summary>What sort of edit a step in <see cref="NoteSurfaceHistory"/> was - which decides whether the next one joins it.</summary>
public enum SurfaceEditKind
{
    /// <summary>Characters typed inside a line. Joins the step before it - see NoteSurfaceHistory.</summary>
    Typing,

    /// <summary>Characters deleted inside a line. Joins the step before it, like typing, but never a typing one.</summary>
    Erasing,

    /// <summary>Lines made, joined, taken away or re-indented. Always a step of its own.</summary>
    Reshaping,

    /// <summary>A box pressed. Always a step of its own.</summary>
    Ticking,

    /// <summary>A paste. Always a step of its own.</summary>
    Pasting
}

/// <summary>
/// Undo and redo for a note's writing surface. The browser has its own, and it is lost here: the surface
/// is a set of line elements the page rebuilds as it goes, and the browser's history cannot follow a
/// document rewritten under it - Ctrl+Z did nothing, or undid something nobody could see. So the surface
/// keeps its own, of whole states (<see cref="SurfaceState"/>, caret included) rather than of changes: a
/// note is small, a copy of it is cheap, and a copy can be put back exactly.
///
/// Typing is kept in steps a person would recognise rather than one per keystroke: characters typed one
/// after another on the same line join one step until a pause of <see cref="PauseEndingAStep"/>, a space
/// that ends a word, a move to another line, or an edit of another kind. Everything else - Enter, a line
/// taken away, a tick, a paste - is a step on its own.
/// </summary>
public sealed class NoteSurfaceHistory
{
    /// <summary>How many steps back can be undone. Far more than anybody presses Ctrl+Z for, and a bound on memory all the same.</summary>
    public const int MaximumSteps = 500;

    public static readonly TimeSpan PauseEndingAStep = TimeSpan.FromSeconds(1);

    private readonly List<SurfaceState> _undo = [];
    private readonly List<SurfaceState> _redo = [];

    /// <summary>The step still taking typing, if the last one was typing - see <see cref="Joins"/>.</summary>
    private OpenStep? _open;

    private sealed record OpenStep(SurfaceEditKind Kind, int Line, double LastAtMilliseconds, bool EndedAWord);

    public NoteSurfaceHistory(SurfaceState initial)
    {
        Current = initial;
    }

    /// <summary>What the surface holds now, as far as this history knows.</summary>
    public SurfaceState Current { get; private set; }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    /// <summary>Starts again from lines that did not come from an edit - a note opened, or a name taken from a suggestion.</summary>
    public void Reset(SurfaceState state)
    {
        _undo.Clear();
        _redo.Clear();
        _open = null;
        Current = state;
    }

    /// <summary>
    /// Records an edit. before is the surface the edit was made on - its selection is where undoing it
    /// puts the caret back - and after is what it left. typed is the text a typing edit put in, which is
    /// how a space is told to end a word. atMilliseconds is when it happened, on any clock that only goes
    /// forward (the browser's event time).
    /// </summary>
    public void Record(SurfaceState before, SurfaceState after, SurfaceEditKind kind, double atMilliseconds, string? typed = null)
    {
        if (after.Lines.SequenceEqual(Current.Lines))
        {
            // Nothing written changed - a caret moved, or a key with nothing to do. Not a step anybody
            // would want to undo.
            Current = after;
            return;
        }

        if (Joins(kind, after, atMilliseconds))
        {
            Current = after;
            _open = _open! with { LastAtMilliseconds = atMilliseconds, EndedAWord = EndsAWord(typed) };
            return;
        }

        Push(_undo, before);
        _redo.Clear();
        Current = after;
        _open = kind is SurfaceEditKind.Typing or SurfaceEditKind.Erasing
            ? new OpenStep(kind, after.Caret.Line, atMilliseconds, EndsAWord(typed))
            : null;
    }

    /// <summary>Puts back the surface as it was before the last step. Null when there is nothing to undo.</summary>
    public SurfaceState? Undo() => Move(from: _undo, to: _redo);

    /// <summary>Puts back the step last undone. Null when there is nothing to redo.</summary>
    public SurfaceState? Redo() => Move(from: _redo, to: _undo);

    private SurfaceState? Move(List<SurfaceState> from, List<SurfaceState> to)
    {
        if (from.Count == 0)
        {
            return null;
        }

        Push(to, Current);
        Current = from[^1];
        from.RemoveAt(from.Count - 1);
        _open = null;
        return Current;
    }

    private bool Joins(SurfaceEditKind kind, SurfaceState after, double atMilliseconds)
        => _open is { } open
            && open.Kind == kind
            && open.Line == after.Caret.Line
            && !open.EndedAWord
            && atMilliseconds - open.LastAtMilliseconds <= PauseEndingAStep.TotalMilliseconds;

    private static bool EndsAWord(string? typed) => !string.IsNullOrEmpty(typed) && string.IsNullOrWhiteSpace(typed);

    private static void Push(List<SurfaceState> steps, SurfaceState state)
    {
        steps.Add(state);
        if (steps.Count > MaximumSteps)
        {
            steps.RemoveAt(0);
        }
    }
}
