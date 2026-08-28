using System.Windows.Input;

namespace Orbit.Maui.Controls;

/// <summary>
/// One pin, used by every list that can pin something - notes, task lists, dashboard cards - so the
/// control means the same thing and looks the same wherever a person meets it. The same reason
/// Orbit.Web has a PinButton component rather than three copies of a glyph.
/// </summary>
public partial class PinButton : ContentView
{
	public static readonly BindableProperty IsPinnedProperty =
		BindableProperty.Create(nameof(IsPinned), typeof(bool), typeof(PinButton), false);

	public static readonly BindableProperty CommandProperty =
		BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(PinButton));

	public static readonly BindableProperty CommandParameterProperty =
		BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(PinButton));

	public PinButton() => InitializeComponent();

	public bool IsPinned
	{
		get => (bool)GetValue(IsPinnedProperty);
		set => SetValue(IsPinnedProperty, value);
	}

	public ICommand? Command
	{
		get => (ICommand?)GetValue(CommandProperty);
		set => SetValue(CommandProperty, value);
	}

	/// <summary>The row being pinned - the command needs to know which one.</summary>
	public object? CommandParameter
	{
		get => GetValue(CommandParameterProperty);
		set => SetValue(CommandParameterProperty, value);
	}
}
