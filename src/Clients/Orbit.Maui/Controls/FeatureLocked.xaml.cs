using System.Windows.Input;

namespace Orbit.Maui.Controls;

/// <summary>
/// The panel shown in place of a locked section. Takes the sentence and the way out as properties
/// rather than reading them from a view model, so every gated screen can use one without them all
/// having to expose the same two members under the same two names.
/// </summary>
public partial class FeatureLocked : ContentView
{
	public static readonly BindableProperty ExplanationProperty =
		BindableProperty.Create(nameof(Explanation), typeof(string), typeof(FeatureLocked), string.Empty);

	public static readonly BindableProperty OpenAccountCommandProperty =
		BindableProperty.Create(nameof(OpenAccountCommand), typeof(ICommand), typeof(FeatureLocked));

	public FeatureLocked() => InitializeComponent();

	/// <summary>What this account cannot use, in a sentence - see LockedFeatureMessage.</summary>
	public string Explanation
	{
		get => (string)GetValue(ExplanationProperty);
		set => SetValue(ExplanationProperty, value);
	}

	public ICommand? OpenAccountCommand
	{
		get => (ICommand?)GetValue(OpenAccountCommandProperty);
		set => SetValue(OpenAccountCommandProperty, value);
	}
}
