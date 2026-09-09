using System.Windows.Input;
using Microsoft.Maui.Controls.Shapes;

namespace Orbit.Maui.Controls;

/// <summary>
/// The circle an errand or a checklist line is ticked off in.
///
/// One control for both, because they were two: an errand marked itself with "○ ✓" and a note's line
/// with "☐ ☑", each at whatever size its screen happened to set. The text faces carry almost none of
/// those glyphs - IBM Plex Sans has the tick and neither box, Space Grotesk has none of them - so
/// Android was substituting a system font and the same act looked different on two screens.
/// </summary>
public partial class CheckCircle : ContentView
{
	public static readonly BindableProperty IsCheckedProperty =
		BindableProperty.Create(nameof(IsChecked), typeof(bool), typeof(CheckCircle), false,
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

		// The tick is a little over half the circle, which is what keeps it looking drawn rather than
		// cropped at every size the three screens ask for.
		Tick.WidthRequest = Diameter * 0.55;
		Tick.HeightRequest = Diameter * 0.55;
		Tick.IsVisible = IsChecked;

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

		Ring.Fill = Brush.Transparent;
		Ring.SetAppTheme(Shape.StrokeProperty, Look("TertiaryTextLight"), Look("TertiaryTextDark"));
	}

	private static Color Look(string key)
		=> Application.Current?.Resources.TryGetValue(key, out var value) is true && value is Color colour
			? colour
			: Colors.Transparent;
}
