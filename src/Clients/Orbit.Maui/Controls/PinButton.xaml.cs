using System.Windows.Input;
using Orbit.Mobile.Localization;

namespace Orbit.Maui.Controls;

/// <summary>
/// One pin, used by every list that can pin something - notes, task lists, dashboard cards - so the
/// control means the same thing and looks the same wherever a person meets it. The same reason
/// Orbit.Web has a PinButton component rather than three copies of a glyph.
/// </summary>
public partial class PinButton : ContentView
{
	public static readonly BindableProperty IsPinnedProperty =
		BindableProperty.Create(
			nameof(IsPinned), typeof(bool), typeof(PinButton), false,
			propertyChanged: (control, _, _) => ((PinButton)control).SayWhatItDoes());

	public static readonly BindableProperty CommandProperty =
		BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(PinButton));

	public static readonly BindableProperty CommandParameterProperty =
		BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(PinButton));

	public PinButton()
	{
		InitializeComponent();
		SayWhatItDoes();
	}

	/// <summary>
	/// A drawn pin says nothing to a screen reader, and which of the two things it offers depends on
	/// the state it is in - so the name follows the state rather than being fixed at "pin".
	/// </summary>
	private void SayWhatItDoes()
	{
		var translations = IPlatformApplication.Current?.Services.GetService<Translations>();
		if (translations is not null)
		{
			SemanticProperties.SetDescription(Tap, translations[IsPinned ? "Unpin" : "Pin"]);
		}
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

	/// <summary>The row being pinned - the command needs to know which one.</summary>
	public object? CommandParameter
	{
		get => GetValue(CommandParameterProperty);
		set => SetValue(CommandParameterProperty, value);
	}
}
