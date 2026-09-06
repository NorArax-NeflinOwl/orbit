using Android.Graphics.Drawables;
using Android.Views;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

namespace Orbit.Maui.Platform;

/// <summary>
/// Dresses a stepper's two buttons as Orbit's own.
///
/// Orbit.Web has no stepper - it writes a number into `.options-number-input`, a small box with a
/// hairline and the quiet fill. MAUI's Stepper exposes no colours at all, so its minus and plus came
/// out as Android's default filled buttons: the one control on a settings screen that did not look
/// like the rest of the app, and dark grey blocks on the light theme.
///
/// Drawn as the app's own quiet button instead - the same surface, hairline, radius and secondary text
/// the implicit Button style sets - so the pair reads as a control of Orbit's rather than a visitor.
/// </summary>
internal static class StepperButtons
{
	/// <summary>app.css's `.options-number-input`: a 7px radius and a hairline, on the quiet fill.</summary>
	private const float Radius = 7;

	public static void DrawOnEveryStepper() => StepperHandler.Mapper.AppendToMapping(
		nameof(IView.Background), (handler, _) => Dress(handler.PlatformView));

	private static void Dress(Android.Views.View? stepper)
	{
		// The platform stepper is a pair of buttons in a container; which container differs between
		// MAUI versions, so the two are found by walking rather than by naming a type that may change.
		foreach (var button in ButtonsIn(stepper))
		{
			var density = button.Context?.Resources?.DisplayMetrics?.Density ?? 1;

			var box = new GradientDrawable();
			box.SetShape(ShapeType.Rectangle);
			box.SetCornerRadius(Radius * density);
			box.SetStroke((int)Math.Round(density), ThemeColours.Hairline.ToPlatform());
			box.SetColor(ThemeColours.Surface.ToPlatform());

			button.Background = box;
			button.SetTextColor(ThemeColours.SubtleText.ToPlatform());
		}
	}

	private static IEnumerable<Android.Widget.Button> ButtonsIn(Android.Views.View? view)
	{
		switch (view)
		{
			case Android.Widget.Button button:
				yield return button;
				break;

			case ViewGroup group:
				for (var child = 0; child < group.ChildCount; child++)
				{
					foreach (var button in ButtonsIn(group.GetChildAt(child)))
					{
						yield return button;
					}
				}

				break;
		}
	}
}
