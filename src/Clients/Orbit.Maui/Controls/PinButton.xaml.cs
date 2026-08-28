using System.Windows.Input;

namespace Orbit.Maui.Controls;

/// <summary>
/// The pin a list row offers, mirroring Orbit.Web's PinButton - see the XAML for the drawing.
///
/// Takes a command rather than raising an event, because every caller is a row inside a
/// CollectionView whose handler lives on the page's view model rather than on the row itself.
/// </summary>
public partial class PinButton : ContentView
{
	public static readonly BindableProperty IsPinnedProperty = BindableProperty.Create(
		nameof(IsPinned), typeof(bool), typeof(PinButton), false, propertyChanged: OnIsPinnedChanged);

	public static readonly BindableProperty CommandProperty = BindableProperty.Create(
		nameof(Command), typeof(ICommand), typeof(PinButton));

	public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(
		nameof(CommandParameter), typeof(object), typeof(PinButton));

	public PinButton()
	{
		InitializeComponent();
		ShowPinnedState();
	}

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

	public object? CommandParameter
	{
		get => GetValue(CommandParameterProperty);
		set => SetValue(CommandParameterProperty, value);
	}

	private static void OnIsPinnedChanged(BindableObject bindable, object oldValue, object newValue)
		=> ((PinButton)bindable).ShowPinnedState();

	/// <summary>
	/// The filled pin is the pinned state and the outlined one the offer to pin, as on the web. Set here
	/// rather than by a converter in the XAML so the fill and the stroke cannot end up different colours.
	/// </summary>
	private void ShowPinnedState() => Pin.Fill = IsPinned ? Pin.Stroke : Brush.Transparent;

	private void OnTapped(object? sender, TappedEventArgs eventArguments)
	{
		if (Command is { } command && command.CanExecute(CommandParameter))
		{
			command.Execute(CommandParameter);
		}
	}
}
