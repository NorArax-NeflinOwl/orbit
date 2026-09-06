namespace Orbit.Maui.Controls;

/// <summary>
/// One setting on a screen of them - see the markup, and Orbit.Web's .options-row, which this is the
/// phone's half of.
/// </summary>
public partial class SettingRow : ContentView
{
	public static readonly BindableProperty TitleProperty = BindableProperty.Create(
		nameof(Title), typeof(string), typeof(SettingRow), string.Empty);

	/// <summary>
	/// What the setting does, under its name. Left out where the name says everything: an empty line
	/// under a title is a gap the eye reads as a missing sentence.
	/// </summary>
	public static readonly BindableProperty DescriptionProperty = BindableProperty.Create(
		nameof(Description), typeof(string), typeof(SettingRow), string.Empty,
		propertyChanged: (row, _, value) =>
			((SettingRow)row).DescriptionLabel.IsVisible = !string.IsNullOrWhiteSpace(value as string));

	/// <summary>The control that changes it - a switch, a picker, a button.</summary>
	public static readonly BindableProperty TrailingProperty = BindableProperty.Create(
		nameof(Trailing), typeof(View), typeof(SettingRow),
		propertyChanged: (row, _, value) => Slot.Fill(((SettingRow)row).TrailingHost, value));

	/// <summary>
	/// The hairline under the row. The web takes it off the last row of a card
	/// (.options-row:last-child); here the caller says so, since a row does not know where it sits.
	/// </summary>
	public static readonly BindableProperty HasDividerProperty = BindableProperty.Create(
		nameof(HasDivider), typeof(bool), typeof(SettingRow), true,
		propertyChanged: (row, _, value) => ((SettingRow)row).Divider.IsVisible = value is true);

	public SettingRow() => InitializeComponent();

	public string Title
	{
		get => (string)GetValue(TitleProperty);
		set => SetValue(TitleProperty, value);
	}

	/// <inheritdoc cref="DescriptionProperty"/>
	public string Description
	{
		get => (string)GetValue(DescriptionProperty);
		set => SetValue(DescriptionProperty, value);
	}

	/// <inheritdoc cref="TrailingProperty"/>
	public View? Trailing
	{
		get => (View?)GetValue(TrailingProperty);
		set => SetValue(TrailingProperty, value);
	}

	/// <inheritdoc cref="HasDividerProperty"/>
	public bool HasDivider
	{
		get => (bool)GetValue(HasDividerProperty);
		set => SetValue(HasDividerProperty, value);
	}
}
