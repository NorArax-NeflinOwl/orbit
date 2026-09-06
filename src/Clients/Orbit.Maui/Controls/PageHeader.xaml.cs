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
		BindableProperty.Create(nameof(Subtitle), typeof(string), typeof(PageHeader), string.Empty,
			propertyChanged: OnSubtitleChanged);

	/// <summary>
	/// The one control that makes another of whatever the screen lists, left of its name - see
	/// Orbit.Web's PageHeader.LeadingAction and the four list screens that carry one.
	/// </summary>
	public static readonly BindableProperty LeadingActionProperty = BindableProperty.Create(
		nameof(LeadingAction), typeof(View), typeof(PageHeader),
		propertyChanged: (header, _, value) => Fill(header, "LeadingHost", value));

	/// <summary>Whatever belongs at the header's other end - how the page is read, rather than what is on it.</summary>
	public static readonly BindableProperty ActionsProperty = BindableProperty.Create(
		nameof(Actions), typeof(View), typeof(PageHeader),
		propertyChanged: (header, _, value) => Fill(header, "ActionsHost", value));

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

	/// <inheritdoc cref="LeadingActionProperty"/>
	public View? LeadingAction
	{
		get => (View?)GetValue(LeadingActionProperty);
		set => SetValue(LeadingActionProperty, value);
	}

	/// <inheritdoc cref="ActionsProperty"/>
	public View? Actions
	{
		get => (View?)GetValue(ActionsProperty);
		set => SetValue(ActionsProperty, value);
	}

	/// <summary>
	/// A screen with nothing to say for itself is just its name: an empty subtitle used to leave a
	/// blank line under every heading, which is the gap the web does not draw.
	/// </summary>
	private static void OnSubtitleChanged(BindableObject bindable, object oldValue, object newValue)
		=> ((PageHeader)bindable).SubtitleLabel.IsVisible = !string.IsNullOrWhiteSpace(newValue as string);

	private static void Fill(BindableObject bindable, string host, object? content)
		=> Slot.Fill((ContentView)((PageHeader)bindable).FindByName(host), content);
}
