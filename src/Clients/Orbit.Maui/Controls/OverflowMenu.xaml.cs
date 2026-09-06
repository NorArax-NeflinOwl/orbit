using System.Windows.Input;

namespace Orbit.Maui.Controls;

/// <summary>
/// Orbit.Web's OverflowMenu, split in two: this is the trigger, and the panel it opens is the page's
/// own <see cref="MenuOverlay"/>. Split because a panel drawn inside a card would be clipped by the
/// row it sits in - so what a press here does is fill the screen's menu, which the overlay is watching.
/// </summary>
public partial class OverflowMenu : ContentView
{
	/// <summary>What filling the menu is - a command on the screen, given the row it was pressed on.</summary>
	public static readonly BindableProperty CommandProperty = BindableProperty.Create(
		nameof(Command), typeof(ICommand), typeof(OverflowMenu));

	public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(
		nameof(CommandParameter), typeof(object), typeof(OverflowMenu));

	public OverflowMenu() => InitializeComponent();

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
}
