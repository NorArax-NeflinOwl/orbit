using System.Runtime.CompilerServices;
using Microsoft.Maui.Handlers;
using Orbit.Maui.Controls;
// Android's View, not MAUI's - both are in scope here and only one of them has a long click.
using AndroidView = Android.Views.View;

namespace Orbit.Maui.Platform;

/// <summary>
/// Reads a hold on the controls that asked for one - see <see cref="LongPress"/>, which is what they ask
/// with, and <c>CheckCircle.LongPressCommand</c>, the one control that does today.
///
/// Android's views have had a long click since the beginning, so there is nothing to build: the listener
/// is hung on the platform view behind the control. Answering it also stops the press that would
/// otherwise follow - a box held to choose it must not also tick itself off - which is what marking a
/// long click handled means on Android.
///
/// Applied through the handler mapper, like <see cref="NoteLineKeyPresses"/> and for the same reason: the
/// controls are built from a template, so there is nowhere to reach one of them by name. Keyed to the
/// button's handler because that is the platform view a pressable control has - <c>CheckCircle</c> draws
/// its circle itself and puts a real Button over it to take the press.
/// </summary>
internal static class LongPresses
{
	/// <summary>
	/// What has already been listened to. A mapper runs again every time the property it is keyed to
	/// changes, so without this every redraw would add another listener - see NoteLineKeyPresses, where
	/// the same table stops one press doing its work as many times as the control had been redrawn.
	/// </summary>
	private static readonly ConditionalWeakTable<AndroidView, EventHandler<AndroidView.LongClickEventArgs>> Listening = [];

	public static void ReadAHoldOnEveryControlThatAsked()
		=> ButtonHandler.Mapper.AppendToMapping(
			nameof(IView.Background),
			(handler, view) => Listen(handler.PlatformView, view));

	/// <param name="pressed">
	/// The platform view, taken as an Android <see cref="AndroidView"/> rather than as a button: the long click is the
	/// view's own, and which button class MAUI builds for a Button is MAUI's business rather than this
	/// file's.
	/// </param>
	private static void Listen(AndroidView? pressed, IView asked)
	{
		if (pressed is null || asked is not BindableObject element)
		{
			return;
		}

		if (Listening.TryGetValue(pressed, out var already))
		{
			pressed.LongClick -= already;
			Listening.Remove(pressed);
		}

		// Only the controls that asked. Every other button in the app keeps Android's own answer to a
		// hold, which is nothing at all.
		if (LongPress.GetCommand(element) is null)
		{
			return;
		}

		void OnHold(object? sender, AndroidView.LongClickEventArgs args)
		{
			// Read again rather than captured: the control is reused as the template redraws, and the row
			// it stands for changes with it.
			if (LongPress.GetCommand(element) is not { } held)
			{
				args.Handled = false;
				return;
			}

			// What the control was already going to hand its own command - the row, for a template that
			// draws one control per row.
			held.Execute((element as Button)?.CommandParameter);

			// Handled, so the press Android would raise next never arrives.
			args.Handled = true;
		}

		EventHandler<AndroidView.LongClickEventArgs> listener = OnHold;
		pressed.LongClick += listener;
		Listening.Add(pressed, listener);
	}
}
