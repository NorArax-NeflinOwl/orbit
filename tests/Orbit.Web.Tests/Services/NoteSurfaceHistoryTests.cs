using Orbit.Contracts.Notes;
using Orbit.Web.Services;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// Undo and redo for a note's writing - see NoteSurfaceHistory. The browser's own history cannot follow
/// a surface whose lines the page rebuilds, so this is the one Ctrl+Z reaches; each test reads what an
/// undo would put back on the surface, caret included.
/// </summary>
public sealed class NoteSurfaceHistoryTests
{
    private static NoteContentLineDto Text(string text) => new(text, IsChecklistItem: false, IsChecked: false);

    private static SurfaceState At(int line, int offset, params string[] lines)
        => SurfaceState.CaretAt([.. lines.Select(Text)], new SurfacePoint(line, offset));

    /// <summary>Types text one character at a time at the end of line 0, a keystroke every stepMilliseconds.</summary>
    private static (SurfaceState State, double At) Type(NoteSurfaceHistory history, SurfaceState from, string text, double startMilliseconds, double stepMilliseconds = 100)
    {
        var state = from;
        var at = startMilliseconds;
        foreach (var character in text)
        {
            var line = state.Lines[0].Text + character;
            var next = At(0, line.Length, line);
            history.Record(state, next, SurfaceEditKind.Typing, at, character.ToString());
            state = next;
            at += stepMilliseconds;
        }

        return (state, at);
    }

    [Fact]
    public void Undo_puts_back_the_surface_before_the_step_with_the_caret_where_the_step_began()
    {
        var before = At(0, 3, "abc");
        var history = new NoteSurfaceHistory(before);
        var after = NoteSurfaceEdits.Enter(before);

        history.Record(before, after, SurfaceEditKind.Reshaping, 0);
        var undone = history.Undo()!;

        Assert.Equal(before.Lines, undone.Lines);
        Assert.Equal(new SurfacePoint(0, 3), undone.Caret);
    }

    [Fact]
    public void Redo_puts_the_step_back_with_the_caret_where_it_left_it()
    {
        var before = At(0, 3, "abc");
        var history = new NoteSurfaceHistory(before);
        var after = NoteSurfaceEdits.Enter(before);
        history.Record(before, after, SurfaceEditKind.Reshaping, 0);

        history.Undo();
        var redone = history.Redo()!;

        Assert.Equal(after.Lines, redone.Lines);
        Assert.Equal(new SurfacePoint(1, 0), redone.Caret);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void Nothing_to_undo_or_redo_answers_nothing()
    {
        var history = new NoteSurfaceHistory(At(0, 0, ""));

        Assert.Null(history.Undo());
        Assert.Null(history.Redo());
    }

    [Fact]
    public void A_word_typed_without_pausing_is_one_step()
    {
        var start = At(0, 0, "");
        var history = new NoteSurfaceHistory(start);

        Type(history, start, "milk", startMilliseconds: 0);

        Assert.Equal([Text("")], history.Undo()!.Lines);
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void A_space_ends_a_word_so_each_word_is_undone_on_its_own()
    {
        var start = At(0, 0, "");
        var history = new NoteSurfaceHistory(start);

        Type(history, start, "buy milk", startMilliseconds: 0);

        Assert.Equal([Text("buy ")], history.Undo()!.Lines);
        Assert.Equal([Text("")], history.Undo()!.Lines);
    }

    [Fact]
    public void A_pause_ends_a_step()
    {
        var start = At(0, 0, "");
        var history = new NoteSurfaceHistory(start);

        var (typed, at) = Type(history, start, "mi", startMilliseconds: 0);
        Type(history, typed, "lk", startMilliseconds: at + NoteSurfaceHistory.PauseEndingAStep.TotalMilliseconds + 1);

        Assert.Equal([Text("mi")], history.Undo()!.Lines);
    }

    [Fact]
    public void Typing_on_another_line_is_another_step()
    {
        var start = At(0, 1, "a", "b");
        var history = new NoteSurfaceHistory(start);
        var first = At(0, 2, "ax", "b");
        var second = At(1, 2, "ax", "by");

        history.Record(start, first, SurfaceEditKind.Typing, 0, "x");
        history.Record(first with { Anchor = new(1, 1), Focus = new(1, 1) }, second, SurfaceEditKind.Typing, 50, "y");
        var undone = history.Undo()!;

        Assert.Equal(first.Lines, undone.Lines);
        Assert.Equal(new SurfacePoint(1, 1), undone.Caret);
    }

    [Fact]
    public void Deleting_after_typing_is_a_step_of_its_own()
    {
        var start = At(0, 0, "");
        var history = new NoteSurfaceHistory(start);
        var (typed, at) = Type(history, start, "milk", startMilliseconds: 0);

        history.Record(typed, At(0, 3, "mil"), SurfaceEditKind.Erasing, at);

        Assert.Equal([Text("milk")], history.Undo()!.Lines);
    }

    [Fact]
    public void A_change_of_shape_never_joins_the_typing_before_it()
    {
        var start = At(0, 0, "");
        var history = new NoteSurfaceHistory(start);
        var (typed, at) = Type(history, start, "milk", startMilliseconds: 0);

        history.Record(typed, NoteSurfaceEdits.Enter(typed), SurfaceEditKind.Reshaping, at);

        Assert.Equal([Text("milk")], history.Undo()!.Lines);
        Assert.Equal([Text("")], history.Undo()!.Lines);
    }

    [Fact]
    public void A_new_edit_after_an_undo_throws_away_what_could_have_been_redone()
    {
        var start = At(0, 0, "");
        var history = new NoteSurfaceHistory(start);
        history.Record(start, At(0, 1, "a"), SurfaceEditKind.Pasting, 0);
        history.Undo();

        history.Record(start, At(0, 1, "b"), SurfaceEditKind.Pasting, 10);

        Assert.False(history.CanRedo);
        Assert.Null(history.Redo());
    }

    [Fact]
    public void An_edit_that_changed_nothing_written_is_not_a_step()
    {
        var start = At(0, 0, "abc");
        var history = new NoteSurfaceHistory(start);

        history.Record(start, At(0, 3, "abc"), SurfaceEditKind.Reshaping, 0);

        Assert.False(history.CanUndo);
        Assert.Equal(new SurfacePoint(0, 3), history.Current.Caret);
    }

    [Fact]
    public void Starting_again_forgets_every_step()
    {
        var start = At(0, 0, "");
        var history = new NoteSurfaceHistory(start);
        history.Record(start, At(0, 1, "a"), SurfaceEditKind.Pasting, 0);

        history.Reset(At(0, 0, "another note"));

        Assert.False(history.CanUndo);
        Assert.Equal([Text("another note")], history.Current.Lines);
    }

    [Fact]
    public void Only_so_many_steps_are_kept()
    {
        var state = At(0, 0, "");
        var history = new NoteSurfaceHistory(state);
        for (var step = 1; step <= NoteSurfaceHistory.MaximumSteps + 5; step++)
        {
            var next = At(0, 0, step.ToString());
            history.Record(state, next, SurfaceEditKind.Pasting, step);
            state = next;
        }

        var undone = 0;
        while (history.Undo() is not null)
        {
            undone++;
        }

        Assert.Equal(NoteSurfaceHistory.MaximumSteps, undone);
    }
}
