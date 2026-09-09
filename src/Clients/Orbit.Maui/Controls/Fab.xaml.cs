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
			propertyChanged: (fab, _, _) => ((Fab)fab).Place());

	/// <summary>
	/// Whether it sits at the top of the screen instead of over its foot.
	///
	/// The foot is where this button belongs on a list, and where the thumb is. A map is the exception:
	/// Android draws its own furniture along the bottom of a map and draws it itself, so nothing in the
	/// markup knows it is there - a column of zoom buttons at the bottom-right, which the crosshair was
	/// covering and taking the presses of, and Google's logo at the bottom-left, which may not be
	/// covered at all. The top-right is the one corner of a map that is the app's to use.
	/// </summary>
	public static readonly BindableProperty IsAtTheTopProperty =
		BindableProperty.Create(nameof(IsAtTheTop), typeof(bool), typeof(Fab), false,
			propertyChanged: (fab, _, _) => ((Fab)fab).Place());

	/// <summary>
	/// Which side it sits on. The right one by default, which is where the thumb of the hand holding
	/// the phone is - the note editor is the one screen with a button in each, and the one on the left
	/// is the lesser of the two, so it is drawn smaller as well.
	/// </summary>
	public static readonly BindableProperty IsOnTheLeftProperty =
		BindableProperty.Create(nameof(IsOnTheLeft), typeof(bool), typeof(Fab), false,
			propertyChanged: (fab, _, _) => ((Fab)fab).Place());

	/// <summary>How far across it is. 56 is the button a screen is for; a second one beside it is 44.</summary>
	public static readonly BindableProperty DiameterProperty =
		BindableProperty.Create(nameof(Diameter), typeof(double), typeof(Fab), 56d,
			propertyChanged: (fab, _, value) => ((Fab)fab).Size((double)value!));

	public Fab()
	{
		InitializeComponent();
		Place();
	}

	/// <summary>
	/// Which corner, and how far off the edges. The bar along the foot is 52 tall where there is one,
	/// and the button sits above the list rather than above the bar. At the top it keeps the same 18
	/// off the side and 12 off the bar above it, and the ad bar is somebody else's problem.
	/// </summary>
	private void Place()
	{
		var across = IsOnTheLeft ? LayoutOptions.Start : LayoutOptions.End;
		HorizontalOptions = across;

		if (IsAtTheTop)
		{
			Margin = IsOnTheLeft ? new Thickness(18, 12, 0, 0) : new Thickness(0, 12, 18, 0);
			VerticalOptions = LayoutOptions.Start;
			Ring.VerticalOptions = LayoutOptions.Start;
			return;
		}

		var down = IsAboveAdBar ? 72 : 20;
		Margin = IsOnTheLeft ? new Thickness(18, 0, 0, down) : new Thickness(0, 0, 18, down);
		VerticalOptions = LayoutOptions.End;
		Ring.VerticalOptions = LayoutOptions.End;
	}

	private void Size(double diameter)
	{
		Ring.WidthRequest = diameter;
		Ring.HeightRequest = diameter;

		if (Ring.StrokeShape is Microsoft.Maui.Controls.Shapes.RoundRectangle corners)
		{
			corners.CornerRadius = diameter / 2;
		}

		// The glyph keeps its share of the circle - 24 of 56 - so a smaller button is a smaller drawing
		// rather than the same drawing crowding a smaller ring.
		Glyph.WidthRequest = diameter * 24 / 56;
		Glyph.HeightRequest = diameter * 24 / 56;
	}

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

	/// <inheritdoc cref="IsAtTheTopProperty"/>
	public bool IsAtTheTop
	{
		get => (bool)GetValue(IsAtTheTopProperty);
		set => SetValue(IsAtTheTopProperty, value);
	}

	/// <inheritdoc cref="IsOnTheLeftProperty"/>
	public bool IsOnTheLeft
	{
		get => (bool)GetValue(IsOnTheLeftProperty);
		set => SetValue(IsOnTheLeftProperty, value);
	}

	/// <inheritdoc cref="DiameterProperty"/>
	public double Diameter
	{
		get => (double)GetValue(DiameterProperty);
		set => SetValue(DiameterProperty, value);
	}

	/// <summary>
	/// Whether the button is showing as pressed - a ring filled in the accent rather than drawn in it.
	/// One button in the app is a switch rather than an action: the note editor's tick-box button, which
	/// stays on while every new line starts with a box. See NoteDetailPage.
	/// </summary>
	public static readonly BindableProperty IsOnProperty =
		BindableProperty.Create(nameof(IsOn), typeof(bool), typeof(Fab), false,
			propertyChanged: (fab, _, value) => ((Fab)fab).ShowWhetherItIsOn((bool)value!));

	/// <inheritdoc cref="IsOnProperty"/>
	public bool IsOn
	{
		get => (bool)GetValue(IsOnProperty);
		set => SetValue(IsOnProperty, value);
	}

	/// <summary>
	/// An Ellipse's Fill rather than the Border's Background: a Border ignores a colour resource on its
	/// background while honouring one on its stroke - see CheckCircle, where the same thing bit.
	/// </summary>
	private void ShowWhetherItIsOn(bool isOn)
	{
		Wash.IsVisible = isOn;
		Wash.SetDynamicResource(Microsoft.Maui.Controls.Shapes.Shape.FillProperty, "Accent");
	}
}
