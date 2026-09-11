using System.Windows.Input;

namespace Orbit.Maui.Controls;

/// <summary>
/// One line of a list of facts - see the markup, and Orbit.Web's Components/Row.razor, which the shape
/// comes from. Used for everything that is read rather than opened as a card: a contact's details, a
/// group's members, an inventory search's answers.
/// </summary>
public partial class Row : ContentView
{
	/// <summary>What the row is about. A row with only one thing on it may leave this unset.</summary>
	public static readonly BindableProperty TitleProperty = BindableProperty.Create(
		nameof(Title), typeof(string), typeof(Row), string.Empty,
		propertyChanged: (row, _, value) =>
			((Row)row).TitleLabel.IsVisible = !string.IsNullOrEmpty(value as string));

	public static readonly BindableProperty LeadingProperty = BindableProperty.Create(
		nameof(Leading), typeof(View), typeof(Row),
		propertyChanged: (row, _, value) => Slot.Fill(((Row)row).LeadingHost, value));

	public static readonly BindableProperty TrailingProperty = BindableProperty.Create(
		nameof(Trailing), typeof(View), typeof(Row),
		propertyChanged: (row, _, value) => Slot.Fill(((Row)row).TrailingHost, value));

	/// <summary>What pressing the row opens. Without one the row is a fact, not a way anywhere.</summary>
	public static readonly BindableProperty PressedCommandProperty = BindableProperty.Create(
		nameof(PressedCommand), typeof(ICommand), typeof(Row),
		propertyChanged: (row, _, _) => ((Row)row).SayWhatItOpens());

	public static readonly BindableProperty PressedCommandParameterProperty = BindableProperty.Create(
		nameof(PressedCommandParameter), typeof(object), typeof(Row),
		propertyChanged: (row, _, _) => ((Row)row).SayWhatItOpens());

	/// <summary>
	/// The hairline under the row. The web takes it off the last row of a list (.list-row:last-child);
	/// here the caller says so, since a list drawn from a template does not know which row it is on.
	/// </summary>
	public static readonly BindableProperty HasDividerProperty = BindableProperty.Create(
		nameof(HasDivider), typeof(bool), typeof(Row), true,
		propertyChanged: (row, _, value) => ((Row)row).Divider.IsVisible = value is true);

	/// <summary>
	/// Something unread is about this row - Orbit.Web's .list-row.row-unseen: the danger colour as a
	/// hairline around it over a wash of the same, and deliberately no pulse of its own.
	/// </summary>
	public static readonly BindableProperty HasNewsProperty = BindableProperty.Create(
		nameof(HasNews), typeof(bool), typeof(Row), false,
		propertyChanged: (row, _, _) => ((Row)row).Mark());

	public Row()
	{
		InitializeComponent();
		TitleLabel.IsVisible = false;

		// The mark's colours are read from the theme in force, so it has to be drawn again when the
		// reader changes it - the same reason the platform mappers hang off the property they paint.
		// Listened to only while the row is on screen: a CollectionView makes and drops these by the
		// dozen, and a subscription nobody takes off is a row that never goes away.
		Loaded += Watch;
		Unloaded += Forget;
	}

	private void Watch(object? sender, EventArgs arguments)
	{
		Application.Current!.RequestedThemeChanged += Redraw;
		Mark();
	}

	private void Forget(object? sender, EventArgs arguments)
		=> Application.Current!.RequestedThemeChanged -= Redraw;

	private void Redraw(object? sender, AppThemeChangedEventArgs arguments) => Mark();

	public string Title
	{
		get => (string)GetValue(TitleProperty);
		set => SetValue(TitleProperty, value);
	}

	/// <summary>An avatar, a colour, a tick - whatever stands before the words.</summary>
	public View? Leading
	{
		get => (View?)GetValue(LeadingProperty);
		set => SetValue(LeadingProperty, value);
	}

	/// <summary>The row's value, and anything else that belongs at its far end.</summary>
	public View? Trailing
	{
		get => (View?)GetValue(TrailingProperty);
		set => SetValue(TrailingProperty, value);
	}

	/// <inheritdoc cref="PressedCommandProperty"/>
	public ICommand? PressedCommand
	{
		get => (ICommand?)GetValue(PressedCommandProperty);
		set => SetValue(PressedCommandProperty, value);
	}

	public object? PressedCommandParameter
	{
		get => GetValue(PressedCommandParameterProperty);
		set => SetValue(PressedCommandParameterProperty, value);
	}

	/// <inheritdoc cref="HasDividerProperty"/>
	public bool HasDivider
	{
		get => (bool)GetValue(HasDividerProperty);
		set => SetValue(HasDividerProperty, value);
	}

	/// <inheritdoc cref="HasNewsProperty"/>
	public bool HasNews
	{
		get => (bool)GetValue(HasNewsProperty);
		set => SetValue(HasNewsProperty, value);
	}

	/// <summary>
	/// The mark itself. The wash is app.css's own 7% of the danger colour; the frame is always there and
	/// always a pixel thick, so gaining the mark cannot move the row or its neighbours.
	/// </summary>
	private void Mark()
	{
		if (!HasNews)
		{
			RowFrame.Stroke = Brush.Transparent;
			RowFrame.BackgroundColor = Colors.Transparent;
			return;
		}

		var danger = Danger();
		RowFrame.Stroke = new SolidColorBrush(danger);
		RowFrame.BackgroundColor = danger.WithAlpha(0.07f);
	}

	private static Color Danger()
	{
		var key = Application.Current?.RequestedTheme == AppTheme.Dark ? "DangerDark" : "DangerLight";
		return Application.Current?.Resources.TryGetValue(key, out var value) is true && value is Color colour
			? colour
			: Colors.Transparent;
	}

	/// <summary>
	/// The invisible button over the row carries the row's own words, or a screen reader lands on a
	/// control with nothing to say.
	/// </summary>
	private void SayWhatItOpens()
	{
		Press.Command = PressedCommand;
		Press.CommandParameter = PressedCommandParameter;
		Press.IsVisible = PressedCommand is not null;
		SemanticProperties.SetDescription(Press, Title);
	}
}
