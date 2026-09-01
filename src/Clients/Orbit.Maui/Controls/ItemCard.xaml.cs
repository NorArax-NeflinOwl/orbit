using System.Windows.Input;

namespace Orbit.Maui.Controls;

/// <summary>
/// The card every list of things is drawn with - see the anatomy in the markup, and Orbit.Web's
/// ItemCard for the same one on the other client.
///
/// Its parts are handed in as views rather than templated, because each list carries something
/// different in them: a note's body is its first line, a task list's is how far through it is, an
/// event's is when it happens. What they share is where those things sit.
/// </summary>
public partial class ItemCard : ContentView
{
	public static readonly BindableProperty NameProperty = BindableProperty.Create(
		nameof(Name), typeof(string), typeof(ItemCard), string.Empty, propertyChanged: OnNameChanged);

	/// <summary>What opens when the card is pressed. Without one the card is a thing to read, not to open.</summary>
	public static readonly BindableProperty OpenCommandProperty = BindableProperty.Create(
		nameof(OpenCommand), typeof(ICommand), typeof(ItemCard), propertyChanged: OnOpenChanged);

	public static readonly BindableProperty OpenCommandParameterProperty = BindableProperty.Create(
		nameof(OpenCommandParameter), typeof(object), typeof(ItemCard), propertyChanged: OnOpenChanged);

	/// <summary>
	/// What the name opens, for a card whose body is a list of things that open elsewhere - Orbit.Web's
	/// OnNameSelected. Where the whole card is the target instead, use OpenCommand.
	/// </summary>
	public static readonly BindableProperty NameCommandProperty = BindableProperty.Create(
		nameof(NameCommand), typeof(ICommand), typeof(ItemCard), propertyChanged: OnNameOpensChanged);

	public static readonly BindableProperty NameCommandParameterProperty = BindableProperty.Create(
		nameof(NameCommandParameter), typeof(object), typeof(ItemCard), propertyChanged: OnNameOpensChanged);

	/// <summary>Something happened here that this reader has not seen - see the mark in the markup.</summary>
	public static readonly BindableProperty HasUnseenActionProperty = BindableProperty.Create(
		nameof(HasUnseenAction), typeof(bool), typeof(ItemCard), false, propertyChanged: OnUnseenChanged);

	public static readonly BindableProperty HandleProperty = BindableProperty.Create(
		nameof(Handle), typeof(View), typeof(ItemCard), propertyChanged: (card, _, value) => Fill(card, "HandleHost", value));

	public static readonly BindableProperty CollapseProperty = BindableProperty.Create(
		nameof(Collapse), typeof(View), typeof(ItemCard), propertyChanged: (card, _, value) => Fill(card, "CollapseHost", value));

	public static readonly BindableProperty PinProperty = BindableProperty.Create(
		nameof(Pin), typeof(View), typeof(ItemCard), propertyChanged: (card, _, value) => Fill(card, "PinHost", value));

	public static readonly BindableProperty MenuProperty = BindableProperty.Create(
		nameof(Menu), typeof(View), typeof(ItemCard), propertyChanged: (card, _, value) => Fill(card, "MenuHost", value));

	public static readonly BindableProperty TagsProperty = BindableProperty.Create(
		nameof(Tags), typeof(View), typeof(ItemCard), propertyChanged: (card, _, value) => Fill(card, "TagsHost", value));

	public static readonly BindableProperty BodyProperty = BindableProperty.Create(
		nameof(Body), typeof(View), typeof(ItemCard), propertyChanged: (card, _, value) => Fill(card, "BodyHost", value));

	public static readonly BindableProperty ExtrasProperty = BindableProperty.Create(
		nameof(Extras), typeof(View), typeof(ItemCard), propertyChanged: (card, _, value) => Fill(card, "ExtrasHost", value));

	public ItemCard() => InitializeComponent();

	/// <summary>The one part every card has.</summary>
	public string Name
	{
		get => (string)GetValue(NameProperty);
		set => SetValue(NameProperty, value);
	}

	public ICommand? OpenCommand
	{
		get => (ICommand?)GetValue(OpenCommandProperty);
		set => SetValue(OpenCommandProperty, value);
	}

	public object? OpenCommandParameter
	{
		get => GetValue(OpenCommandParameterProperty);
		set => SetValue(OpenCommandParameterProperty, value);
	}

	public bool HasUnseenAction
	{
		get => (bool)GetValue(HasUnseenActionProperty);
		set => SetValue(HasUnseenActionProperty, value);
	}

	public ICommand? NameCommand
	{
		get => (ICommand?)GetValue(NameCommandProperty);
		set => SetValue(NameCommandProperty, value);
	}

	public object? NameCommandParameter
	{
		get => GetValue(NameCommandParameterProperty);
		set => SetValue(NameCommandParameterProperty, value);
	}

	/// <summary>Moving the card up or down, where the order is the reader's to set.</summary>
	public View? Handle
	{
		get => (View?)GetValue(HandleProperty);
		set => SetValue(HandleProperty, value);
	}

	/// <summary>Folding the card down to its heading, where there is a body to fold away.</summary>
	public View? Collapse
	{
		get => (View?)GetValue(CollapseProperty);
		set => SetValue(CollapseProperty, value);
	}

	/// <summary>Keeping this card at the top of its list, where that is the reader's to decide.</summary>
	public View? Pin
	{
		get => (View?)GetValue(PinProperty);
		set => SetValue(PinProperty, value);
	}

	/// <summary>What can be done to the thing without opening it.</summary>
	public View? Menu
	{
		get => (View?)GetValue(MenuProperty);
		set => SetValue(MenuProperty, value);
	}

	/// <summary>Short facts about the thing - how much it matters, who shared it.</summary>
	public View? Tags
	{
		get => (View?)GetValue(TagsProperty);
		set => SetValue(TagsProperty, value);
	}

	/// <summary>What the thing is, in its own words: a first line, a count, a time.</summary>
	public View? Body
	{
		get => (View?)GetValue(BodyProperty);
		set => SetValue(BodyProperty, value);
	}

	/// <summary>What the card says about itself rather than about its subject - when it last changed.</summary>
	public View? Extras
	{
		get => (View?)GetValue(ExtrasProperty);
		set => SetValue(ExtrasProperty, value);
	}

	private static void OnNameChanged(BindableObject bindable, object oldValue, object newValue)
	{
		var card = (ItemCard)bindable;
		card.NameLabel.Text = newValue as string ?? string.Empty;
		card.SayWhatItOpens();
	}

	private static void OnNameOpensChanged(BindableObject bindable, object oldValue, object newValue)
	{
		var card = (ItemCard)bindable;
		card.NameButton.Command = card.NameCommand;
		card.NameButton.CommandParameter = card.NameCommandParameter;
		card.NameButton.IsVisible = card.NameCommand is not null;
		card.SayWhatItOpens();
	}

	private static void OnUnseenChanged(BindableObject bindable, object oldValue, object newValue)
		=> ((ItemCard)bindable).ActionMark.IsVisible = newValue is true;

	private static void OnOpenChanged(BindableObject bindable, object oldValue, object newValue)
	{
		var card = (ItemCard)bindable;
		card.OpenButton.Command = card.OpenCommand;
		card.OpenButton.CommandParameter = card.OpenCommandParameter;
		card.OpenButton.IsVisible = card.OpenCommand is not null;
		card.SayWhatItOpens();
	}

	/// <summary>
	/// The invisible button over the card carries the name, or a screen reader lands on a control with
	/// nothing to say - see SpokenNameTests, which is why every control here has something.
	/// </summary>
	private void SayWhatItOpens()
	{
		SemanticProperties.SetDescription(OpenButton, Name);
		SemanticProperties.SetDescription(NameButton, Name);
	}

	private static void Fill(BindableObject bindable, string host, object? content)
	{
		var card = (ItemCard)bindable;
		Slot.Fill((ContentView)card.FindByName(host), content);
	}
}
