using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Contracts.Notes;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Screens.Notes;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Crypto;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// The screen a note opens into, which until now did not exist: tapping a note on the list did nothing
/// at all, so the phone could list notes and never read one.
/// </summary>
public sealed class NoteDetailScreenTests
{
    [Fact]
    public async Task A_note_opens_showing_what_it_says()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk", "bread");

        var screen = await context.OpenAsync(note.LocalId);

        Assert.Equal("Shopping", screen.Title);
        Assert.Equal(["milk", "bread"], screen.Lines.Select(line => line.Text));
    }

    /// <summary>
    /// Enter at the end of a line starts the next one, which is what the editor is: one surface being
    /// typed on rather than a field with an Add button beside it.
    /// </summary>
    [Fact]
    public async Task A_line_added_is_kept()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk");
        var screen = await context.OpenAsync(note.LocalId);

        screen.AddLineAfter(screen.Lines[0]).Text = "eggs";
        await screen.SaveLinesCommand.ExecuteAsync(null);

        Assert.Equal(["milk", "eggs"], screen.Lines.Select(line => line.Text));
        Assert.Equal(["milk", "eggs"], (await context.Notes.FindAsync(note.LocalId))!.Content.Select(line => line.Text));
    }

    /// <summary>
    /// A new line keeps the indentation of the one above it. A list stays a list when a line is added
    /// to the middle of it, which is the first thing an editor doing anything else gets wrong.
    /// </summary>
    [Fact]
    public async Task A_new_line_starts_where_the_one_above_it_starts()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "\t\tmilk");
        var screen = await context.OpenAsync(note.LocalId);

        Assert.Equal("\t\t", screen.AddLineAfter(screen.Lines[0]).Text);
    }

    /// <summary>
    /// The design gives this editor no toolbar: the reader types "[]" where they want a box and gets
    /// one. The mark comes back out of the text, because what it meant is now carried by the line.
    /// </summary>
    [Theory]
    [InlineData("[]milk", "milk")]
    [InlineData("[] milk", "milk")]
    [InlineData("[ ] milk", "milk")]
    [InlineData("\t[] milk", "\tmilk")]
    public async Task Typing_a_pair_of_brackets_puts_a_tick_box_on_the_line(string typed, string left)
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", string.Empty);
        var screen = await context.OpenAsync(note.LocalId);

        screen.Lines[0].Text = typed;

        Assert.True(screen.Lines[0].IsChecklistItem);
        Assert.Equal(left, screen.Lines[0].Text);
        // And the next line carries on the list, which is what somebody writing one wants.
        Assert.True(screen.IsWritingAChecklist);
    }

    /// <summary>
    /// A line was a read-only label on this screen, so anything written into a note could never be
    /// corrected - the note had to be deleted and written again. Orbit.Web edits its lines in place.
    /// </summary>
    [Fact]
    public async Task A_line_can_be_corrected_after_it_is_written()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "mikl");
        var screen = await context.OpenAsync(note.LocalId);

        screen.Lines[0].Text = "milk";
        await screen.SaveLinesCommand.ExecuteAsync(null);

        var reopened = await context.OpenAsync(note.LocalId);
        Assert.Equal(["milk"], reopened.Lines.Select(line => line.Text));
    }

    /// <summary>
    /// Leaving the screen abandons the edit, which is how a reader decides against a change: the button
    /// in the corner is what writes the note down, and it used to mean nothing because leaving saved
    /// anyway. Every other detail screen already closes without writing.
    /// </summary>
    [Fact]
    public async Task A_line_edited_and_left_alone_is_not_written_down()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "mikl");
        var screen = await context.OpenAsync(note.LocalId);

        screen.Lines[0].Text = "milk";
        await screen.CloseAsync();

        var reopened = await context.OpenAsync(note.LocalId);
        Assert.Equal(["mikl"], reopened.Lines.Select(line => line.Text));
    }

    /// <summary>And the same for the name, which Save writes along with the lines.</summary>
    [Fact]
    public async Task A_name_edited_and_left_alone_is_not_written_down_either()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk");
        var screen = await context.OpenAsync(note.LocalId);

        screen.Title = "Shopping list";
        await screen.CloseAsync();

        Assert.Equal("Shopping", (await context.OpenAsync(note.LocalId)).Title);
    }

    /// <summary>
    /// What the question at the door reads - see NoteDetailPage.MayLeaveAsync. Compared against what
    /// was last written rather than tracked with a flag, so a letter typed and deleted again leaves
    /// nothing to ask about.
    /// </summary>
    [Fact]
    public async Task A_note_knows_whether_leaving_would_lose_anything()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk");
        var screen = await context.OpenAsync(note.LocalId);

        Assert.False(screen.HasUnsavedChanges);

        screen.Lines[0].Text = "milk and bread";
        Assert.True(screen.HasUnsavedChanges);

        screen.Lines[0].Text = "milk";
        Assert.False(screen.HasUnsavedChanges);

        screen.Title = "Shopping list";
        Assert.True(screen.HasUnsavedChanges);

        await screen.SaveLinesCommand.ExecuteAsync(null);
        Assert.False(screen.HasUnsavedChanges);
    }

    /// <summary>A tick is a change like any other, and it is not written until Save either.</summary>
    [Fact]
    public async Task Ticking_a_line_is_something_leaving_would_lose()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk");
        var screen = await context.OpenAsync(note.LocalId);
        screen.ToggleChecklistCommand.Execute(screen.Lines[0]);
        await screen.SaveLinesCommand.ExecuteAsync(null);

        screen.ToggleCheckedCommand.Execute(screen.Lines[0]);

        Assert.True(screen.HasUnsavedChanges);
        await screen.CloseAsync();
        Assert.False((await context.OpenAsync(note.LocalId)).Lines[0].IsChecked);
    }

    /// <summary>Pressing Save is what commits both of them.</summary>
    [Fact]
    public async Task Save_writes_the_name_and_the_lines_together()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "mikl");
        var screen = await context.OpenAsync(note.LocalId);

        screen.Title = "Shopping list";
        screen.Lines[0].Text = "milk";
        await screen.SaveLinesCommand.ExecuteAsync(null);

        var reopened = await context.OpenAsync(note.LocalId);
        Assert.Equal("Shopping list", reopened.Title);
        Assert.Equal(["milk"], reopened.Lines.Select(line => line.Text));
    }

    /// <summary>
    /// Two lines that say the same thing are still two lines. They were compared by value, so ticking
    /// one ticked both - and deleting one deleted both.
    /// </summary>
    [Fact]
    public async Task Two_lines_that_say_the_same_thing_are_told_apart()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk", "milk");
        var screen = await context.OpenAsync(note.LocalId);

        screen.ToggleChecklistCommand.Execute(screen.Lines[0]);
        screen.ToggleCheckedCommand.Execute(screen.Lines[0]);

        Assert.True(screen.Lines[0].IsChecked);
        Assert.False(screen.Lines[1].IsChecklistItem);
    }

    /// <summary>
    /// A line gives the same three answers an errand does: nothing, done, given up on - see TickState.
    /// A line somebody gave up on is finished with and not done, which is why it is not simply ticked.
    /// </summary>
    [Fact]
    public async Task A_line_can_be_crossed_out_rather_than_ticked_off()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk");
        var screen = await context.OpenAsync(note.LocalId);
        screen.ToggleChecklistCommand.Execute(screen.Lines[0]);

        screen.ToggleCheckedCommand.Execute(screen.Lines[0]);
        screen.ToggleCheckedCommand.Execute(screen.Lines[0]);

        Assert.False(screen.Lines[0].IsChecked);
        Assert.True(screen.Lines[0].IsFailed);
        // Struck through either way: the circle beside it says which of the two it was.
        Assert.True(screen.Lines[0].IsCompleted);
    }

    /// <summary>
    /// The button in the editor's bottom-left corner is a switch: while it is on, every line started
    /// begins with an empty box. Pressing it is what turns it on - see NoteDetailPage.
    /// </summary>
    [Fact]
    public async Task While_the_tick_box_button_is_on_every_new_line_starts_with_a_box()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk");
        var screen = await context.OpenAsync(note.LocalId);

        screen.ToggleChecklistCommand.Execute(screen.Lines[0]);
        var next = screen.AddLineAfter(screen.Lines[0]);

        Assert.True(next.IsChecklistItem);
        Assert.False(next.IsChecked);
    }

    [Fact]
    public async Task A_line_can_become_a_checklist_item_and_be_ticked()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk");
        var screen = await context.OpenAsync(note.LocalId);

        screen.ToggleChecklistCommand.Execute(screen.Lines[0]);
        screen.ToggleCheckedCommand.Execute(screen.Lines[0]);

        Assert.True(screen.Lines[0].IsChecklistItem);
        Assert.True(screen.Lines[0].IsChecked);
    }

    /// <summary>
    /// Ticking is only for checklist items. Prose has nothing to tick, and letting it through would
    /// write a checked line that the editor then shows with no box beside it.
    /// </summary>
    [Fact]
    public async Task Prose_cannot_be_ticked()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk");
        var screen = await context.OpenAsync(note.LocalId);

        screen.ToggleCheckedCommand.Execute(screen.Lines[0]);

        Assert.False(screen.Lines[0].IsChecked);
    }

    /// <summary>
    /// Backspace at the head of a line joins it to the one above, which is how a line is got rid of on
    /// a surface with no per-line menu - and what any text field does. The caret lands where the two
    /// met rather than at the end of what was pulled up.
    /// </summary>
    [Fact]
    public async Task Backspace_at_the_head_of_a_line_joins_it_to_the_one_above()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk", "bread");
        var screen = await context.OpenAsync(note.LocalId);

        var landing = screen.MergeIntoTheLineAbove(screen.Lines[1]);

        Assert.Equal(["milkbread"], screen.Lines.Select(line => line.Text));
        Assert.Equal(4, landing!.Value.Caret);
    }

    /// <summary>
    /// A line with a tick box loses the box first, and only a second press joins it upwards. It is the
    /// one way to undo a box from the keyboard - a reader who typed "[]" by accident would otherwise
    /// have to reach for the button in the corner.
    /// </summary>
    [Fact]
    public async Task Backspace_at_the_head_of_a_tickable_line_takes_the_box_off_before_it_joins_anything()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk", "bread");
        var screen = await context.OpenAsync(note.LocalId);
        screen.Lines[1].IsChecklistItem = true;
        screen.Lines[1].IsChecked = true;

        Assert.Null(screen.MergeIntoTheLineAbove(screen.Lines[1]));

        Assert.False(screen.Lines[1].IsChecklistItem);
        Assert.False(screen.Lines[1].IsChecked);
        Assert.Equal(["milk", "bread"], screen.Lines.Select(line => line.Text));

        // And now it joins, as any plain line does.
        Assert.NotNull(screen.MergeIntoTheLineAbove(screen.Lines[1]));
        Assert.Equal(["milkbread"], screen.Lines.Select(line => line.Text));
    }

    /// <summary>
    /// The arrows walk the caret from one line to the next. The editor is a column of one-line fields,
    /// so left to Android the key stopped at the ends of the one it was in and the only way from a line
    /// to its neighbour was to reach up and press it.
    /// </summary>
    [Fact]
    public async Task The_arrows_reach_the_line_above_and_the_line_below()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk", "bread", "eggs");
        var screen = await context.OpenAsync(note.LocalId);

        Assert.Same(screen.Lines[0], screen.TheLineAbove(screen.Lines[1]));
        Assert.Same(screen.Lines[2], screen.TheLineBelow(screen.Lines[1]));
    }

    /// <summary>
    /// Nothing over the first line - the page takes the caret to the note's name, which is the writing's
    /// own first line - and nothing at all under the last: the arrow walks the writing that is there and
    /// does not start a line, which is Enter's job and nothing else's.
    /// </summary>
    [Fact]
    public async Task An_arrow_off_either_end_of_the_writing_reaches_no_line()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk", "bread");
        var screen = await context.OpenAsync(note.LocalId);

        Assert.Null(screen.TheLineAbove(screen.Lines[0]));
        Assert.Null(screen.TheLineBelow(screen.Lines[^1]));
        Assert.Equal(2, screen.Lines.Count);
    }

    /// <summary>
    /// Enter in the middle of a line breaks the line: what follows the caret moves down onto the new
    /// one, which is what a text field does everywhere.
    /// </summary>
    [Fact]
    public async Task Enter_carries_whatever_follows_the_caret_down_onto_the_new_line()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk and bread");
        var screen = await context.OpenAsync(note.LocalId);

        var fresh = screen.AddLineAfter(screen.Lines[0], "milk".Length);

        Assert.Equal(["milk", " and bread"], screen.Lines.Select(line => line.Text));
        Assert.Same(screen.Lines[1], fresh);
    }

    /// <summary>
    /// A checklist goes on being a checklist without the button in the corner being touched - and an
    /// empty line ends it, which is how a reader stops one.
    /// </summary>
    [Fact]
    public async Task A_tickable_line_starts_another_until_one_is_left_empty()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk");
        var screen = await context.OpenAsync(note.LocalId);
        screen.Lines[0].IsChecklistItem = true;

        var second = screen.AddLineAfter(screen.Lines[0]);
        Assert.True(second.IsChecklistItem);

        // Enter on the empty one it just made: nothing on either side of the caret, so the list ends.
        var third = screen.AddLineAfter(second);
        Assert.False(third.IsChecklistItem);
    }

    /// <summary>And it means nothing on the first line, which has nothing above it to join.</summary>
    [Fact]
    public async Task Backspace_on_the_first_line_does_nothing()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk", "bread");
        var screen = await context.OpenAsync(note.LocalId);

        Assert.Null(screen.MergeIntoTheLineAbove(screen.Lines[0]));
        Assert.Equal(["milk", "bread"], screen.Lines.Select(line => line.Text));
    }

    /// <summary>
    /// A private note nothing here can open - no key on this device. Offering an editor over that would
    /// present an empty note and, on save, send the emptiness back over the sealed copy.
    /// </summary>
    [Fact]
    public async Task A_private_note_this_device_cannot_open_stays_read_only_and_says_why()
    {
        using var context = new ScreenContext(PrivateContent.SignedInWithoutAKey(Owner));
        var note = await context.AddSealedNoteAsync();

        var screen = await context.OpenAsync(note.LocalId);

        Assert.True(screen.IsReadOnly);
        Assert.False(screen.CanEdit);
        Assert.NotEqual(string.Empty, screen.ReadOnlyReason);
    }

    /// <summary>
    /// The point of the whole exercise: the phone can now make a note private, and what it writes down
    /// is what the server is allowed to hold - nothing readable, and a sealed payload beside it.
    /// </summary>
    [Fact]
    public async Task Making_a_note_private_seals_its_words_and_leaves_the_readable_columns_empty()
    {
        using var context = new ScreenContext(PrivateContent.HoldingAKeyFor(Owner));
        var note = await context.AddNoteAsync("Bank details", "sort code");
        var screen = await context.OpenAsync(note.LocalId);

        screen.IsPrivate = true;
        await screen.SaveLinesCommand.ExecuteAsync(null);

        var stored = context.Stored(note.LocalId);
        Assert.True(stored.IsPrivate);
        Assert.Equal(string.Empty, stored.Title);
        Assert.Empty(stored.Content);
        Assert.NotNull(stored.EncryptedContent);
    }

    [Fact]
    public async Task A_note_this_device_sealed_opens_again_with_its_words_back()
    {
        using var context = new ScreenContext(PrivateContent.HoldingAKeyFor(Owner));
        var note = await context.AddNoteAsync("Bank details", "sort code");
        var screen = await context.OpenAsync(note.LocalId);
        screen.IsPrivate = true;
        await screen.SaveLinesCommand.ExecuteAsync(null);

        var reopened = await context.OpenAsync(note.LocalId);

        Assert.False(reopened.IsReadOnly);
        Assert.True(reopened.IsPrivate);
        Assert.Equal("Bank details", reopened.Title);
        Assert.Equal(["sort code"], reopened.Lines.Select(line => line.Text));
    }

    /// <summary>
    /// A private note is offered to nobody: the server holds no readable copy to hand over, which is
    /// what being private means. Orbit.Web hides the same panel for the same reason.
    /// </summary>
    [Fact]
    public async Task A_private_note_is_not_offered_to_anybody()
    {
        using var context = new ScreenContext(PrivateContent.HoldingAKeyFor(Owner));
        var note = await context.AddNoteAsync("Bank details", "sort code");
        var screen = await context.OpenAsync(note.LocalId);
        screen.IsPrivate = true;
        await screen.SaveLinesCommand.ExecuteAsync(null);

        var reopened = await context.OpenAsync(note.LocalId);

        Assert.False(reopened.Share.CanShare);
    }

    /// <summary>
    /// The way back from private, and the half that was missing: clearing the switch puts the title and
    /// the lines where the server can read them again and drops the sealed payload.
    ///
    /// This test was removed on 2026-08-31 for failing about one full-suite run in ten and never on its
    /// own. The cause was answered the next day by something else: "Give every local-store context a
    /// connection of its own" (afd0f7e2), which says in its own words that sharing one connection made
    /// SQLite refuse EF's user-function registration while a statement was open, and that the failure
    /// "arrived only under load, on CI, in a test about something else entirely" - which is exactly the
    /// shape this one had. Nobody came back for it. See TestDoubles/LocalStore.
    /// </summary>
    [Fact]
    public async Task Turning_private_off_puts_the_words_back_where_the_server_can_read_them()
    {
        using var context = new ScreenContext(PrivateContent.HoldingAKeyFor(Owner));
        var note = await context.AddNoteAsync("Bank details", "sort code");
        var screen = await context.OpenAsync(note.LocalId);
        screen.IsPrivate = true;
        await screen.SaveLinesCommand.ExecuteAsync(null);

        var reopened = await context.OpenAsync(note.LocalId);
        reopened.IsPrivate = false;
        await reopened.SaveLinesCommand.ExecuteAsync(null);

        var stored = context.Stored(note.LocalId);
        Assert.False(stored.IsPrivate);
        Assert.Equal("Bank details", stored.Title);
        Assert.Null(stored.EncryptedContent);
    }

    /// <summary>
    /// Sealing needs the account's own key, and the key gate is where a device without one gets it -
    /// the same place chat sends people. Saving the words in the clear instead would break the promise
    /// the switch had just made.
    /// </summary>
    [Fact]
    public async Task Making_a_note_private_without_a_key_asks_for_it_rather_than_saving()
    {
        using var context = new ScreenContext(PrivateContent.SignedInWithoutAKey(Owner));
        var note = await context.AddNoteAsync("Bank details", "sort code");
        var screen = await context.OpenAsync(note.LocalId);

        screen.IsPrivate = true;
        await screen.SaveLinesCommand.ExecuteAsync(null);

        Assert.Contains(nameof(IScreenNavigator.ShowChatKeyGate), context.Navigator.Destinations);
        Assert.False(context.Stored(note.LocalId).IsPrivate);
    }

    [Fact]
    public async Task Deleting_a_note_goes_back_to_the_list()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk");
        var screen = await context.OpenAsync(note.LocalId);

        await screen.DeleteCommand.ExecuteAsync(null);

        Assert.Contains(nameof(IScreenNavigator.ShowNotes), context.Navigator.Destinations);
        Assert.Empty(await context.Notes.GetAllAsync());
    }

    /// <summary>Whoever is signed in - only its identity matters, as the key is kept per account.</summary>
    private static readonly Guid Owner = Guid.Parse("11111111-0000-4000-8000-000000000001");

    /// <summary>
    /// The way out of a refusal, rather than only being told about one. Somebody on a train who has
    /// something to write down about a shared note may write it down - into a copy of their own, which
    /// nobody else can be editing and which therefore breaks no rule the policy exists to keep.
    /// </summary>
    [Fact]
    public async Task A_note_that_cannot_be_changed_offline_offers_a_copy_instead()
    {
        using var context = new ScreenContext();
        var note = await context.AddSharedNoteAsync("Team shopping", "milk");
        context.Network.Becomes(false);

        var screen = await context.OpenAsync(note.LocalId);

        Assert.True(screen.IsReadOnly);
        Assert.True(screen.IsCopyOffered);
    }

    /// <summary>
    /// Online, and still read-only: this is not the offline policy but what the owner allowed. Found by
    /// opening a note claimed from a public link, which is granted ReadOnly and nothing more - it opened
    /// as an ordinary editable screen, and the edit was lost minutes later when the server refused it.
    /// </summary>
    [Fact]
    public async Task A_note_shared_to_read_is_read_only_even_with_a_connection()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteSharedToReadAsync("Somebody else's note", "milk");

        var screen = await context.OpenAsync(note.LocalId);

        Assert.True(screen.IsReadOnly);
        // And said as something no connection will fix, unlike the offline refusals.
        Assert.Contains("Ask whoever shared it", screen.ReadOnlyReason);
        Assert.DoesNotContain("online", screen.ReadOnlyReason);
    }

    /// <summary>
    /// A copy is the way out of a refusal that will pass. This one does not pass, and a copy of it could
    /// never be kept over the original - the server would refuse that too.
    /// </summary>
    [Fact]
    public async Task A_note_shared_to_read_is_not_offered_a_copy_to_edit()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteSharedToReadAsync("Somebody else's note", "milk");

        var screen = await context.OpenAsync(note.LocalId);

        Assert.False(screen.IsCopyOffered);
    }

    [Fact]
    public async Task A_note_that_can_be_changed_is_not_asked_about_at_all()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Mine alone", "milk");
        context.Network.Becomes(false);

        var screen = await context.OpenAsync(note.LocalId);

        Assert.False(screen.IsReadOnly);
        Assert.False(screen.IsCopyOffered);
    }

    /// <summary>
    /// Nothing readable to copy, and a copy is written in the clear - so the offer is not made. Being
    /// told why it is read-only is all this screen can honestly give.
    /// </summary>
    [Fact]
    public async Task A_sealed_note_is_never_offered_a_copy()
    {
        using var context = new ScreenContext(PrivateContent.SignedInWithoutAKey(Owner));
        var note = await context.AddSealedNoteAsync();
        context.Network.Becomes(false);

        var screen = await context.OpenAsync(note.LocalId);

        Assert.True(screen.IsReadOnly);
        Assert.False(screen.IsCopyOffered);
    }

    [Fact]
    public async Task Taking_the_copy_opens_it_with_the_words_that_were_there()
    {
        using var context = new ScreenContext();
        var note = await context.AddSharedNoteAsync("Team shopping", "milk", "bread");
        context.Network.Becomes(false);
        var screen = await context.OpenAsync(note.LocalId);

        await screen.CopyForEditingCommand.ExecuteAsync(null);

        var copy = Assert.Single(await context.Notes.GetCopiesOfAsync(note.LocalId));
        Assert.Equal("Team shopping", copy.Title);
        Assert.Equal(["milk", "bread"], copy.Content.Select(line => line.Text));
        Assert.Equal(copy.LocalId, context.Navigator.LastNoteId);
    }

    /// <summary>
    /// The copy is this phone's own and shared with nobody, so the very policy that refused the
    /// original allows it - which is the whole point, and would be silently untrue if a copy inherited
    /// the original's sharing.
    /// </summary>
    [Fact]
    public async Task The_copy_can_be_written_on_with_no_connection()
    {
        using var context = new ScreenContext();
        var note = await context.AddSharedNoteAsync("Team shopping", "milk");
        context.Network.Becomes(false);
        var screen = await context.OpenAsync(note.LocalId);
        await screen.CopyForEditingCommand.ExecuteAsync(null);
        var copy = Assert.Single(await context.Notes.GetCopiesOfAsync(note.LocalId));

        var copyScreen = await context.OpenAsync(copy.LocalId);
        copyScreen.AddLineAfter(copyScreen.Lines[^1]).Text = "bread";
        await copyScreen.SaveLinesCommand.ExecuteAsync(null);

        Assert.False(copyScreen.IsReadOnly);
        Assert.Equal(["milk", "bread"], copyScreen.Lines.Select(line => line.Text));
    }

    /// <summary>Asked once. A reader who says no is reading, and being asked again is being nagged.</summary>
    [Fact]
    public async Task Declining_puts_the_question_away()
    {
        using var context = new ScreenContext();
        var note = await context.AddSharedNoteAsync("Team shopping", "milk");
        context.Network.Becomes(false);
        var screen = await context.OpenAsync(note.LocalId);

        screen.DeclineCopyCommand.Execute(null);

        Assert.False(screen.IsCopyOffered);
        Assert.True(screen.IsReadOnly);
    }

    /// <summary>A copy of a copy is a chain nobody could review; the offer stops at one.</summary>
    [Fact]
    public async Task A_copy_is_not_itself_offered_a_copy()
    {
        using var context = new ScreenContext();
        var note = await context.AddSharedNoteAsync("Team shopping", "milk");
        context.Network.Becomes(false);
        var screen = await context.OpenAsync(note.LocalId);
        await screen.CopyForEditingCommand.ExecuteAsync(null);
        var copy = Assert.Single(await context.Notes.GetCopiesOfAsync(note.LocalId));

        // Shared, so the policy would refuse it - if a copy ever arrived shared, it still gets no offer.
        await using (var dbContext = context.Store.CreateDbContext())
        {
            dbContext.Notes.Single(candidate => candidate.LocalId == copy.LocalId).IsShared = true;
            await dbContext.SaveChangesAsync();
        }

        Assert.False((await context.OpenAsync(copy.LocalId)).IsCopyOffered);
    }

    /// <summary>
    /// The left half of the editor's foot: when the note last changed, in the words its card on the list
    /// uses - see LastChanged. It was left out because the screen kept no such state; the row had it.
    /// </summary>
    [Fact]
    public async Task The_foot_says_when_the_note_last_changed()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Groceries", "Milk");

        var screen = await context.OpenAsync(note.LocalId);

        Assert.Equal("Today", screen.Footnote);
    }

    /// <summary>And whose it is, when it is not the reader's own - the same words the list's card says.</summary>
    [Fact]
    public async Task The_foot_of_a_note_shared_in_says_who_shared_it()
    {
        using var context = new ScreenContext();
        var note = await context.AddSharedNoteAsync("Groceries", "Milk");
        await using (var dbContext = context.Store.CreateDbContext())
        {
            dbContext.Notes.Single(candidate => candidate.LocalId == note.LocalId).SharedByUserName = "ala";
            await dbContext.SaveChangesAsync();
        }

        var screen = await context.OpenAsync(note.LocalId);

        Assert.Equal("Shared by ala · Today", screen.Footnote);
    }

    private sealed class ScreenContext : IDisposable
    {
        private readonly LocalStore _localStore = new();
        private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-08-27T10:00:00Z"));
        private readonly NoteSynchronizer _synchronizer;
        private readonly PrivateContentSealer _privateContent;

        public ScreenContext(PrivateContentSealer? privateContent = null)
        {
            _privateContent = privateContent ?? PrivateContent.WithoutAKey();
            Server = new FakeNotesServer(_clock);
            Notes = new LocalNoteRepository(_localStore, _clock, Network, _privateContent);
            _synchronizer = new NoteSynchronizer(
                _localStore, new NotesClient(Server.ToHttpClient()), _clock, new SyncGate(),
                NullLogger<NoteSynchronizer>.Instance);
        }

        /// <summary>Whether the phone has a connection, which is what the offline refusal turns on.</summary>
        public FixedNetworkStatus Network { get; } = FixedNetworkStatus.Online;

        /// <summary>
        /// A note somebody else shared in, which is the one kind the offline policy refuses - see
        /// OfflineEditPolicy.
        /// </summary>
        public async Task<LocalNote> AddSharedNoteAsync(string title, params string[] lines)
        {
            var note = await AddNoteAsync(title, lines);
            await using var dbContext = _localStore.CreateDbContext();
            dbContext.Notes.Single(candidate => candidate.LocalId == note.LocalId).IsShared = true;
            await dbContext.SaveChangesAsync();
            return note;
        }

        /// <summary>
        /// One shared in without permission to change it - what a public link grants, and what the
        /// screen used to open as an ordinary editable note.
        /// </summary>
        public async Task<LocalNote> AddNoteSharedToReadAsync(string title, params string[] lines)
        {
            var note = await AddSharedNoteAsync(title, lines);
            await using var dbContext = _localStore.CreateDbContext();
            dbContext.Notes.Single(candidate => candidate.LocalId == note.LocalId).AccessLevel = "ReadOnly";
            await dbContext.SaveChangesAsync();
            return note;
        }

        /// <summary>The row as it really sits in the database, rather than as a read hands it back opened.</summary>
        public LocalNote Stored(Guid localId)
        {
            using var dbContext = _localStore.CreateDbContext();
            return dbContext.Notes.Single(note => note.LocalId == localId);
        }

        public FakeNotesServer Server { get; }

        /// <summary>The database itself, for the few tests that must arrange a row a screen cannot.</summary>
        public LocalStore Store => _localStore;

        public LocalNoteRepository Notes { get; }

        public RecordingScreenNavigator Navigator { get; } = new();

        public Task<LocalNote> AddNoteAsync(string title, params string[] lines)
            => Notes.CreateAsync(title, [.. lines.Select(line => new NoteContentLineDto(line, false, false))]);

        /// <summary>
        /// As one arrives from the server: private, with the title and lines stripped and only a sealed
        /// payload left - see Orbit.Core.Notes.Note.ReadableOrSealed. The payload is nonsense on purpose:
        /// these tests are about a note this device cannot open, and one sealed under a replaced key pair
        /// is indistinguishable from it.
        /// </summary>
        public async Task<LocalNote> AddSealedNoteAsync()
        {
            var note = await Notes.CreateAsync(string.Empty, []);
            await using var dbContext = _localStore.CreateDbContext();
            var stored = dbContext.Notes.Single(candidate => candidate.LocalId == note.LocalId);
            stored.IsPrivate = true;
            stored.EncryptedCiphertext = "AAAA";
            stored.EncryptedNonce = "BBBB";
            await dbContext.SaveChangesAsync();
            return note;
        }

        public async Task<NoteDetailViewModel> OpenAsync(Guid localId)
        {
            var screen = new NoteDetailViewModel(
                Notes, _synchronizer, new NotesClient(Server.ToHttpClient()), NothingIsBeingEdited(_clock),
                new Translations(new InMemoryLanguageStore()), _privateContent,
                ShareTestPanel.For(_localStore, new ChatRepository(_localStore, _clock)), Navigator,
                new LocalFolderRepository(_localStore, _clock), _clock);

            screen.Open(localId);
            await screen.LoadCommand.ExecuteAsync(null);
            return screen;
        }

        /// <summary>
        /// A lock over a fake server that answers every claim with "yours" - these tests are about the
        /// editor, and EditLockTests covers what happens when somebody else is in it.
        /// </summary>
        private static EditLock NothingIsBeingEdited(TimeProvider clock)
            => new(FixedNetworkStatus.Online, clock, new Translations(new InMemoryLanguageStore()));

        public void Dispose()
        {
            Server.Dispose();
            _localStore.Dispose();
        }
    }
}
