using System.Runtime.CompilerServices;
using Android.Views;
using Android.Widget;
using Microsoft.Maui.Handlers;
using Orbit.Maui.Controls;

namespace Orbit.Maui.Platform;

/// <summary>
/// Makes backspace at the head of one of the note editor's lines join it to the line above.
///
/// MAUI has no key events of its own, so this is the one place the press can be seen: Android's own
/// EditText raises it, and the field says what it means by carrying a command - see
/// <see cref="NoteLineKeys"/>, which is what the page attaches.
///
/// Applied through the handler mapper, like <see cref="FieldBox"/> and for the same reason: the fields
/// are built from a template, so there is nowhere to reach one of them by name.
/// </summary>
internal static class NoteLineBackspace
{
	/// <summary>
	/// What has already been listened to. A mapper runs again every time the property it is keyed to
	/// changes, so without this every redraw would add another listener and one press would merge as
	/// many lines as the field had been redrawn.
	/// </summary>
	private static readonly ConditionalWeakTable<EditText, EventHandler<Android.Views.View.KeyEventArgs>> Listening = [];

	public static void JoinLinesOnEveryNoteField()
		=> EntryHandler.Mapper.AppendToMapping(
			nameof(IView.Background),
			(handler, view) => Listen(handler.PlatformView, view));

	private static void Listen(EditText? field, IView asked)
	{
		if (field is null || asked is not BindableObject element)
		{
			return;
		}

		if (Listening.TryGetValue(field, out var already))
		{
			field.KeyPress -= already;
			Listening.Remove(field);
		}

		// Only the fields that asked. Every other Entry in the app keeps Android's own backspace.
		if (NoteLineKeys.GetJoinsTheLineAbove(element) is null)
		{
			return;
		}

		void OnKey(object? sender, Android.Views.View.KeyEventArgs args)
		{
			args.Handled = false;

			if (args.KeyCode != Keycode.Del
				|| args.Event?.Action != KeyEventActions.Down
				|| field.SelectionStart != 0
				|| field.SelectionEnd != 0)
			{
				return;
			}

			// Read again rather than captured: the field is reused as the template redraws, and the row
			// it stands for - and so the line it would join - changes with it.
			if (NoteLineKeys.GetJoinsTheLineAbove(element) is not { } join)
			{
				return;
			}

			// The field itself, because one command serves every line - it is the page's, bound by the
			// template, and this is the only thing that says which line the press came from.
			join.Execute(element);
			args.Handled = true;
		}

		EventHandler<Android.Views.View.KeyEventArgs> listener = OnKey;
		field.KeyPress += listener;
		Listening.Add(field, listener);
	}
}
