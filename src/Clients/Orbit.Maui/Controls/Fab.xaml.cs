using System.Windows.Input;
using Microsoft.Maui.Controls.Shapes;

namespace Orbit.Maui.Controls;

/// <summary>
/// The floating action button: the one thing a list screen is for, within reach of a thumb.
///
/// Takes its drawing rather than fixing one, because the same button means "add a note" on one screen
/// and "find me" on the map. The plus is the default because that is what most of them are.
/// </summary>
public partial class Fab : ContentView
{
	public static readonly BindableProperty DataProperty =
		BindableProperty.Create(nameof(Data), typeof(Geometry), typeof(Fab),
			propertyChanged: (fab, _, value) => ((Fab)fab).Glyph.Data = value as Geometry);

	public static readonly BindableProperty CommandProperty =
		BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(Fab),
			propertyChanged: (fab, _, value) => ((Fab)fab).Press.Command = value as ICommand);

	public static readonly BindableProperty CommandParameterProperty =
		BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(Fab),
			propertyChanged: (fab, _, value) => ((Fab)fab).Press.CommandParameter = value);

	/// <summary>
	/// What the button is for, in words. Required: a button with no name at all is a circle to a screen
	/// reader, and SpokenNameTests would refuse it in any case.
	/// </summary>
	public static readonly BindableProperty DescriptionProperty =
		BindableProperty.Create(nameof(Description), typeof(string), typeof(Fab), string.Empty,
			propertyChanged: (fab, _, value) => SemanticProperties.SetDescription(((Fab)fab).Press, (string?)value ?? string.Empty));

	/// <summary>
	/// Whether the screen carries the ad bar under it. The button sits above the list, not above the
	/// bar, so where there is one it moves up by its height - 52 plus the 20 it already keeps off the
	/// bottom edge.
	/// </summary>
	public static readonly BindableProperty IsAboveAdBarProperty =
		BindableProperty.Create(nameof(IsAboveAdBar), typeof(bool), typeof(Fab), false,
			propertyChanged: (fab, _, value) => ((Fab)fab).Margin = new Thickness(0, 0, 18, (bool)value! ? 72 : 20));

	public Fab() => InitializeComponent();

	/// <summary>Path data on a 24x24 canvas, as everything else in the bar and the drawer is.</summary>
	[System.ComponentModel.TypeConverter(typeof(PathGeometryConverter))]
	public Geometry? Data
	{
		get => (Geometry?)GetValue(DataProperty);
		set => SetValue(DataProperty, value);
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

	/// <inheritdoc cref="IsAboveAdBarProperty"/>
	public bool IsAboveAdBar
	{
		get => (bool)GetValue(IsAboveAdBarProperty);
		set => SetValue(IsAboveAdBarProperty, value);
	}
}
