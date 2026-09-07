namespace Orbit.Maui.Controls;

/// <summary>
/// The row every list of people and groups is drawn with - see the anatomy in the markup, and
/// Orbit.Web's PersonRow for the same one on the other client.
///
/// Opening what the row names is left to the list rather than offered here: these lists are already
/// CollectionViews whose selection opens a conversation, and a second way to press the same row would
/// mean two ways to arrive at it and one of them going stale.
/// </summary>
public partial class PersonRow : ContentView
{
	/// <summary>
	/// Whose row this is - the person or the group. Decides the avatar's colour, which is theirs and
	/// stays theirs however the list is sorted or filtered.
	/// </summary>
	public static readonly BindableProperty IdProperty = BindableProperty.Create(
		nameof(Id), typeof(Guid), typeof(PersonRow), Guid.Empty, propertyChanged: OnWhoChanged);

	public static readonly BindableProperty NameProperty = BindableProperty.Create(
		nameof(Name), typeof(string), typeof(PersonRow), string.Empty, propertyChanged: OnWhoChanged);

	/// <summary>The quieter second line - a login, or how many people are in a group.</summary>
	public static readonly BindableProperty SubtitleProperty = BindableProperty.Create(
		nameof(Subtitle), typeof(string), typeof(PersonRow), string.Empty, propertyChanged: OnSubtitleChanged);

	/// <summary>
	/// Where somebody is, by the server's PresenceStatus name. Empty for a group: a group is not
	/// somewhere anybody is or is not, so its avatar carries no dot.
	/// </summary>
	public static readonly BindableProperty StatusProperty = BindableProperty.Create(
		nameof(Status), typeof(string), typeof(PersonRow), string.Empty, propertyChanged: OnStatusChanged);

	/// <summary>Something is waiting here that this reader has not seen - an unread message.</summary>
	public static readonly BindableProperty HasUnseenActionProperty = BindableProperty.Create(
		nameof(HasUnseenAction), typeof(bool), typeof(PersonRow), false,
		propertyChanged: (row, _, value) => ((PersonRow)row).ActionMark.IsVisible = value is true);

	/// <summary>Keeping this row at the top of its list, where that is the reader's to decide.</summary>
	public static readonly BindableProperty PinProperty = BindableProperty.Create(
		nameof(Pin), typeof(View), typeof(PersonRow),
		propertyChanged: (row, _, value) => Slot.Fill(((PersonRow)row).PinHost, value));

	/// <summary>What can be done to the conversation without opening it.</summary>
	public static readonly BindableProperty MenuProperty = BindableProperty.Create(
		nameof(Menu), typeof(View), typeof(PersonRow),
		propertyChanged: (row, _, value) => Slot.Fill(((PersonRow)row).MenuHost, value));

	public PersonRow() => InitializeComponent();

	public Guid Id
	{
		get => (Guid)GetValue(IdProperty);
		set => SetValue(IdProperty, value);
	}

	public string Name
	{
		get => (string)GetValue(NameProperty);
		set => SetValue(NameProperty, value);
	}

	public string Subtitle
	{
		get => (string)GetValue(SubtitleProperty);
		set => SetValue(SubtitleProperty, value);
	}

	public string Status
	{
		get => (string)GetValue(StatusProperty);
		set => SetValue(StatusProperty, value);
	}

	public bool HasUnseenAction
	{
		get => (bool)GetValue(HasUnseenActionProperty);
		set => SetValue(HasUnseenActionProperty, value);
	}

	public View? Pin
	{
		get => (View?)GetValue(PinProperty);
		set => SetValue(PinProperty, value);
	}

	public View? Menu
	{
		get => (View?)GetValue(MenuProperty);
		set => SetValue(MenuProperty, value);
	}

	/// <summary>The row's own name, beside the circle that is made of the same two things.</summary>
	private static void OnWhoChanged(BindableObject bindable, object oldValue, object newValue)
	{
		var row = (PersonRow)bindable;

		row.NameLabel.Text = row.Name;
		row.Face.Id = row.Id;
		row.Face.Name = row.Name;
	}

	private static void OnSubtitleChanged(BindableObject bindable, object oldValue, object newValue)
	{
		var row = (PersonRow)bindable;
		row.SubtitleLabel.Text = newValue as string ?? string.Empty;
		row.SubtitleLabel.IsVisible = !string.IsNullOrWhiteSpace(row.SubtitleLabel.Text);
	}

	private static void OnStatusChanged(BindableObject bindable, object oldValue, object newValue)
		=> ((PersonRow)bindable).Face.Status = newValue as string ?? string.Empty;
}
