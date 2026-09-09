using System.Windows.Input;
using Microsoft.Maui.Controls.Shapes;

namespace Orbit.Maui.Controls;

/// <summary>
/// One row of the <see cref="Drawer"/>: a drawing, a name, and how much is waiting behind it.
///
/// A control rather than eight copies of the same eleven lines of markup, which is what the drawer's
/// first draft was. The chosen state is drawn here so that "where am I" looks the same on every row -
/// the failure the old bar had, where six icons all took the accent whether or not their section was
/// the one on screen.
/// </summary>
public partial class DrawerEntry : ContentView
{
	public static readonly BindableProperty IconProperty =
		BindableProperty.Create(nameof(Icon), typeof(Geometry), typeof(DrawerEntry), propertyChanged: Redraw);

	public static readonly BindableProperty TextProperty =
		BindableProperty.Create(nameof(Text), typeof(string), typeof(DrawerEntry), string.Empty, propertyChanged: Redraw);

	public static readonly BindableProperty IsChosenProperty =
		BindableProperty.Create(nameof(IsChosen), typeof(bool), typeof(DrawerEntry), false, propertyChanged: Redraw);

	/// <summary>
	/// What is waiting, as the count is already written for the bar - empty when there is nothing, which
	/// is what leaves the pill out rather than drawing an empty one.
	/// </summary>
	public static readonly BindableProperty BadgeProperty =
		BindableProperty.Create(nameof(Badge), typeof(string), typeof(DrawerEntry), string.Empty, propertyChanged: Redraw);

	public static readonly BindableProperty CommandProperty =
		BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(DrawerEntry), propertyChanged: Redraw);

	public DrawerEntry() => InitializeComponent();

	/// <summary>Path data on a 24x24 canvas, so one size serves every section.</summary>
	[System.ComponentModel.TypeConverter(typeof(PathGeometryConverter))]
	public Geometry? Icon
	{
		get => (Geometry?)GetValue(IconProperty);
		set => SetValue(IconProperty, value);
	}

	public string Text
	{
		get => (string)GetValue(TextProperty);
		set => SetValue(TextProperty, value);
	}

	public bool IsChosen
	{
		get => (bool)GetValue(IsChosenProperty);
		set => SetValue(IsChosenProperty, value);
	}

	public string Badge
	{
		get => (string)GetValue(BadgeProperty);
		set => SetValue(BadgeProperty, value);
	}

	public ICommand? Command
	{
		get => (ICommand?)GetValue(CommandProperty);
		set => SetValue(CommandProperty, value);
	}

	private static void Redraw(BindableObject bindable, object? oldValue, object? newValue)
		=> ((DrawerEntry)bindable).Redraw();

	private void Redraw()
	{
		Glyph.Data = Icon;
		Name.Text = Text;
		Press.Command = Command;
		SemanticProperties.SetDescription(Press, Text);

		Count.IsVisible = !string.IsNullOrEmpty(Badge);
		CountLabel.Text = Badge;

		// Taken off before it is set, not only on the branch that wants nothing: SetDynamicResource
		// registers the property against the dictionary and keeps it registered, so a later SetAppTheme
		// on the same property is painted over the next time the dictionary is read - which is every
		// theme switch. The same trap ItemCard.Edge fell into.
		Name.RemoveDynamicResource(Microsoft.Maui.Controls.Label.TextColorProperty);
		Surface.RemoveDynamicResource(VisualElement.BackgroundColorProperty);

		if (IsChosen)
		{
			Name.SetDynamicResource(Microsoft.Maui.Controls.Label.TextColorProperty, "Accent");
			Surface.SetDynamicResource(BackgroundColorProperty, "AccentSubtle");
			return;
		}

		Name.SetAppThemeColor(
			Microsoft.Maui.Controls.Label.TextColorProperty,
			Look("TextPrimaryLight"),
			Look("TextPrimaryDark"));
		Surface.BackgroundColor = Colors.Transparent;
	}

	private static Color Look(string key)
		=> Application.Current?.Resources.TryGetValue(key, out var value) is true && value is Color colour
			? colour
			: Colors.Transparent;
}
