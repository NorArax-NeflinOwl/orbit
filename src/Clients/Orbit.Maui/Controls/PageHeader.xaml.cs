namespace Orbit.Maui.Controls;

/// <summary>
/// What a screen says about itself before its content starts: a line on what it holds, the control
/// that makes another of them, and whatever belongs at the other end.
///
/// The name is not here any more - the top bar draws it, from the page's own Title, so that every
/// screen names itself in exactly one place and the bar is never blank. What is left is the rest of
/// the old header, and it is on the way out too: the design has no header at all, and each screen
/// loses this as it is redrawn.
///
/// Taken as properties rather than read from a view model, so a screen can have one without every view
/// model having to expose the same members under the same names - the same reason FeatureLocked takes
/// its sentence that way.
/// </summary>
public partial class PageHeader : ContentView
{
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

	/// <summary>An empty subtitle is left out rather than drawn as a blank line.</summary>
	private static void OnSubtitleChanged(BindableObject bindable, object oldValue, object newValue)
		=> ((PageHeader)bindable).SubtitleLabel.IsVisible = !string.IsNullOrWhiteSpace(newValue as string);

	private static void Fill(BindableObject bindable, string host, object? content)
		=> Slot.Fill((ContentView)((PageHeader)bindable).FindByName(host), content);
}
