using System.Windows.Input;
using Microsoft.Maui.Controls.Shapes;

namespace Orbit.Maui.Controls;

/// <summary>
/// The circle an errand or a checklist line is ticked off in.
///
/// One control for both, because they were two: an errand marked itself with "○ ✓" and a note's line
/// with "☐ ☑", each at whatever size its screen happened to set. Neither pair survives the redesign's
/// faces - Lora and Cormorant Garamond have none of those four glyphs, so Android was substituting a
/// system font and the same act looked different on two screens.
/// </summary>
public partial class CheckCircle : ContentView
{
	public static readonly BindableProperty IsCheckedProperty =
		BindableProperty.Create(nameof(IsChecked), typeof(bool), typeof(CheckCircle), false,
			propertyChanged: (circle, _, _) => ((CheckCircle)circle).Redraw());

	/// <summary>
	/// Crossed out rather than ticked off: the entry, or the line, was finished with and not done - see
	/// Orbit.Core.Tasks.TaskItem.IsFailed. Drawn in the colour everything that went wrong is drawn in,
	/// with a cross in place of the tick, so the two are told apart at a glance down a list.
	/// </summary>
	public static readonly BindableProperty IsFailedProperty =
		BindableProperty.Create(nameof(IsFailed), typeof(bool), typeof(CheckCircle), false,
			propertyChanged: (circle, _, _) => ((CheckCircle)circle).Redraw());

	/// <summary>
	/// How big the circle is. 22 on a list of errands, 26 on the one entry a screen is about, 20 on a
	/// note's line - the design sizes it by how much the thing being ticked matters.
	/// </summary>
	public static readonly BindableProperty DiameterProperty =
		BindableProperty.Create(nameof(Diameter), typeof(double), typeof(CheckCircle), 22d,
			propertyChanged: (circle, _, _) => ((CheckCircle)circle).Redraw());

	/// <summary>
	/// What ticking it does. A circle given none - a line of somebody else's shared list, which is
	/// shown rather than offered - keeps its drawing and loses its press, so a screen reader reads it
	/// as a state and does not announce it as something to activate.
	/// </summary>
	public static readonly BindableProperty CommandProperty =
		BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(CheckCircle),
			propertyChanged: (circle, _, value) =>
			{
				var press = ((CheckCircle)circle).Press;
				press.Command = value as ICommand;
				press.IsEnabled = value is not null;
			});

	public static readonly BindableProperty CommandParameterProperty =
		BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(CheckCircle),
			propertyChanged: (circle, _, value) => ((CheckCircle)circle).Press.CommandParameter = value);

	/// <summary>What ticking this off would mean, for a screen reader. Required by SpokenNameTests.</summary>
	public static readonly BindableProperty DescriptionProperty =
		BindableProperty.Create(nameof(Description), typeof(string), typeof(CheckCircle), string.Empty,
			propertyChanged: (circle, _, value) =>
				SemanticProperties.SetDescription(((CheckCircle)circle).Press, (string?)value ?? string.Empty));

	public CheckCircle()
	{
		InitializeComponent();
		Redraw();
	}

	public bool IsChecked
	{
		get => (bool)GetValue(IsCheckedProperty);
		set => SetValue(IsCheckedProperty, value);
	}

	/// <inheritdoc cref="IsFailedProperty"/>
	public bool IsFailed
	{
		get => (bool)GetValue(IsFailedProperty);
		set => SetValue(IsFailedProperty, value);
	}

	/// <inheritdoc cref="DiameterProperty"/>
	public double Diameter
	{
		get => (double)GetValue(DiameterProperty);
		set => SetValue(DiameterProperty, value);
	}

	public ICommand? Command
	{
		get => (ICommand?)GetValue(CommandProperty);
		set => SetValue(CommandProperty, value);
	}

	public object? CommandParameter
	{
		get => GetValue(CommandParameterProperty);
		set => SetValue(CommandParameterProperty, value);
	}

	/// <inheritdoc cref="DescriptionProperty"/>
	public string Description
	{
		get => (string)GetValue(DescriptionProperty);
		set => SetValue(DescriptionProperty, value);
	}

	private void Redraw()
	{
		WidthRequest = Diameter;
		HeightRequest = Diameter;
		Ring.WidthRequest = Diameter;
		Ring.HeightRequest = Diameter;

		// The mark is a little over half the circle, which is what keeps it looking drawn rather than
		// cropped at every size the three screens ask for.
		Tick.WidthRequest = Diameter * 0.55;
		Tick.HeightRequest = Diameter * 0.55;
		Tick.IsVisible = IsChecked || IsFailed;
		// Two marks out of one Path: a tick, or the cross of something given up on. Drawn rather than
		// written for the reason the class comment gives - neither glyph exists in the faces this app
		// is set in.
		Tick.Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString(
			IsChecked ? TickMark : CrossMark)!;

		// Both cleared first, every time. Two different traps meet on these two properties:
		//
		// A dynamic resource stays registered against the property until it is removed, and paints
		// itself back every time the dictionary is read - which ItemCard.Edge and DrawerEntry both had
		// to be written around.
		//
		// And a *local* value beats a dynamic resource outright, so a circle that had once been set
		// plainly transparent could never take the accent again. That is why neither is written in the
		// markup and why ClearValue follows the removal.
		Ring.RemoveDynamicResource(Shape.FillProperty);
		Ring.RemoveDynamicResource(Shape.StrokeProperty);
		Ring.ClearValue(Shape.FillProperty);
		Ring.ClearValue(Shape.StrokeProperty);

		if (IsChecked)
		{
			Ring.SetDynamicResource(Shape.FillProperty, "Accent");
			Ring.SetDynamicResource(Shape.StrokeProperty, "Accent");
			return;
		}

		if (IsFailed)
		{
			// The theme's own warning colour rather than the accent: done and given up on must not be
			// the same circle with a different mark inside it.
			Ring.SetAppTheme(Shape.FillProperty, Look("WarningLight"), Look("WarningDark"));
			Ring.SetAppTheme(Shape.StrokeProperty, Look("WarningLight"), Look("WarningDark"));
			return;
		}

		Ring.Fill = Brush.Transparent;
		Ring.SetAppTheme(Shape.StrokeProperty, Look("TertiaryTextLight"), Look("TertiaryTextDark"));
	}

	/// <summary>The tick, as the XAML draws it - see CheckCircle.xaml.</summary>
	private const string TickMark = "M5,12 L10,17 L19,7";

	/// <summary>The cross, on the same 24-wide box the tick is drawn in.</summary>
	private const string CrossMark = "M6,6 L18,18 M18,6 L6,18";

	private static Color Look(string key)
		=> Application.Current?.Resources.TryGetValue(key, out var value) is true && value is Color colour
			? colour
			: Colors.Transparent;
}
