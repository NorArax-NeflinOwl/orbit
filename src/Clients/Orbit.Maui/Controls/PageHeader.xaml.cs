namespace Orbit.Maui.Controls;

/// <summary>
/// A screen's name and the line under it, as Orbit.Web's .page-header carries them. Taken as
/// properties rather than read from a view model, so a screen can have one without every view model
/// having to expose the same two members under the same two names - the same reason FeatureLocked
/// takes its sentence that way.
/// </summary>
public partial class PageHeader : ContentView
{
	public static readonly BindableProperty TitleProperty =
		BindableProperty.Create(nameof(Title), typeof(string), typeof(PageHeader), string.Empty);

	public static readonly BindableProperty SubtitleProperty =
		BindableProperty.Create(nameof(Subtitle), typeof(string), typeof(PageHeader), string.Empty);

	public PageHeader() => InitializeComponent();

	public string Title
	{
		get => (string)GetValue(TitleProperty);
		set => SetValue(TitleProperty, value);
	}

	/// <summary>One line on what the screen holds. Empty leaves the screen with just its name.</summary>
	public string Subtitle
	{
		get => (string)GetValue(SubtitleProperty);
		set => SetValue(SubtitleProperty, value);
	}
}
