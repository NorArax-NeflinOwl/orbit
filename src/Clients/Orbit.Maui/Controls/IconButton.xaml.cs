using System.Windows.Input;
using Microsoft.Maui.Controls.Shapes;

namespace Orbit.Maui.Controls;

/// <summary>
/// Orbit.Web's .icon-btn - a drawing that does something, with no word beside it. The drawings
/// themselves stay with the screen that uses them, as path data on a 20x20 canvas; this only says how
/// big the button is, whether it carries an edge, and what pressing it does.
/// </summary>
public partial class IconButton : ContentView
{
	/// <summary>The drawing, as path data on the same 20x20 canvas every other Orbit icon is authored on.</summary>
	public static readonly BindableProperty DataProperty = BindableProperty.Create(
		nameof(Data), typeof(Geometry), typeof(IconButton));

	public static readonly BindableProperty CommandProperty = BindableProperty.Create(
		nameof(Command), typeof(ICommand), typeof(IconButton),
		propertyChanged: (button, _, value) => ((IconButton)button).Press.Command = value as ICommand);

	public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(
		nameof(CommandParameter), typeof(object), typeof(IconButton),
		propertyChanged: (button, _, value) => ((IconButton)button).Press.CommandParameter = value);

	/// <summary>What a screen reader says, and what the web puts in the button's title.</summary>
	public static readonly BindableProperty DescriptionProperty = BindableProperty.Create(
		nameof(Description), typeof(string), typeof(IconButton), string.Empty,
		propertyChanged: (button, _, value) =>
			SemanticProperties.SetDescription(((IconButton)button).Press, value as string ?? string.Empty));

	public static readonly BindableProperty VariantProperty = BindableProperty.Create(
		nameof(Variant), typeof(IconButtonVariant), typeof(IconButton), IconButtonVariant.Plain,
		propertyChanged: (button, _, _) => ((IconButton)button).Redraw());

	/// <summary>
	/// Nothing to do yet - a Save with nothing written, say. The accent edge goes with it, because an
	/// edge saying "this is the one" over a button that refuses the press is the edge lying. The web
	/// writes the same rule as .page-action-primary:disabled.
	/// </summary>
	public static readonly BindableProperty IsEnabledForPressProperty = BindableProperty.Create(
		nameof(IsEnabledForPress), typeof(bool), typeof(IconButton), true,
		propertyChanged: (button, _, _) => ((IconButton)button).Redraw());

	public IconButton()
	{
		InitializeComponent();
		Redraw();
	}

	/// <remarks>
	/// The converter is named here rather than left to the property's type: a Path's own Data has one
	/// hung off it by the framework, and without it every caller would have to write the drawing out
	/// as a PathGeometry element instead of the one line of path data it is.
	/// </remarks>
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

	/// <inheritdoc cref="VariantProperty"/>
	public IconButtonVariant Variant
	{
		get => (IconButtonVariant)GetValue(VariantProperty);
		set => SetValue(VariantProperty, value);
	}

	/// <inheritdoc cref="IsEnabledForPressProperty"/>
	public bool IsEnabledForPress
	{
		get => (bool)GetValue(IsEnabledForPressProperty);
		set => SetValue(IsEnabledForPressProperty, value);
	}

	/// <summary>
	/// Which of the four looks this is, as the two styles that draw it. Styles rather than properties
	/// set here, because the accent is a resource the reader can change while a screen is open - and
	/// only a DynamicResource inside a style follows it.
	/// </summary>
	private void Redraw()
	{
		var isOutlined = Variant is IconButtonVariant.Add or IconButtonVariant.ActionPrimary;
		var isAction = Variant is IconButtonVariant.Action or IconButtonVariant.ActionPrimary;
		var isAccented = isOutlined && IsEnabledForPress;

		Frame.Style = Look<Style>(isAction
			? isAccented ? "PageActionPrimaryBorder" : "PageActionBorder"
			: isAccented ? "PageAddBorder" : "IconButtonFrame");

		Glyph.Style = Look<Style>(isAction
			? isAccented ? "PageActionPathPrimary" : "PageActionPath"
			: isAccented ? "IconPathAccent" : "IconPath");

		Press.IsEnabled = IsEnabledForPress;
		Opacity = IsEnabledForPress ? 1 : 0.55;
	}

	private static T? Look<T>(string key) where T : class
		=> Application.Current?.Resources.TryGetValue(key, out var value) is true ? value as T : null;
}
