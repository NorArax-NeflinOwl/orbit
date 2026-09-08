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
		nameof(Extras), typeof(View), typeof(ItemCard),
		propertyChanged: (card, _, value) => ((ItemCard)card).Foot(value as View));

	/// <summary>
	/// A colour this card is about - an event's own. Taken as the string the event stores rather than
	/// as a Color, because that is what travels: an event with none set has null here and the card
	/// draws no strip at all, which is not the same as drawing one in the accent.
	/// </summary>
	public static readonly BindableProperty AccentColourProperty = BindableProperty.Create(
		nameof(AccentColour), typeof(string), typeof(ItemCard),
		propertyChanged: (card, _, value) => ((ItemCard)card).PaintTheEdge(value as string));

	/// <summary>
	/// Kept at the top of its list by the reader. Marked by its edge rather than by moving it
	/// somewhere else - it has already moved to the top, and saying so twice is noise.
	/// </summary>
	public static readonly BindableProperty IsPinnedProperty = BindableProperty.Create(
		nameof(IsPinned), typeof(bool), typeof(ItemCard), false,
		propertyChanged: (card, _, _) => ((ItemCard)card).Edge());

	/// <summary>
	/// The name the breathing edge is committed under, so it can be called off again - see Pulse.
	/// </summary>
	private const string NewsPulse = "card-with-news";

	public ItemCard()
	{
		InitializeComponent();

		// A card scrolled out of the list goes on breathing otherwise: a CollectionView keeps its cells
		// alive and reuses them, and an animation nobody can see is a frame budget nobody gets back.
		Unloaded += (_, _) => this.AbortAnimation(NewsPulse);
		Loaded += (_, _) => Edge();
		Shape();
	}

	/// <summary>
	/// Whether this is a card or a row.
	///
	/// A card has an edge all the way round and sits in its own space; a row has a hairline above it
	/// and nothing else, and the list it is in reads as one column of writing rather than a stack of
	/// boxes. The dashboard's sections are cards - they are containers, and each holds a list of its
	/// own. Everything a list is *made of* is a row.
	/// </summary>
	public static readonly BindableProperty IsBorderedProperty =
		BindableProperty.Create(nameof(IsBordered), typeof(bool), typeof(ItemCard), true,
			propertyChanged: (card, _, _) => ((ItemCard)card).Shape());

	/// <inheritdoc cref="IsBorderedProperty"/>
	public bool IsBordered
	{
		get => (bool)GetValue(IsBorderedProperty);
		set => SetValue(IsBorderedProperty, value);
	}

	/// <summary>
	/// A row gives back everything a card spends on being a box: the side and bottom edges, the corner
	/// radius, the gap between one and the next, and the padding that kept the writing off the edge.
	/// What is left is the hairline above it, which is the only thing separating two rows.
	/// </summary>
	private void Shape()
	{
		TopRule.IsVisible = !IsBordered;
		FooterRule.IsVisible = IsBordered;
		Frame.StrokeThickness = IsBordered ? 1 : 0;
		Frame.Margin = IsBordered ? new Thickness(0, 5) : new Thickness(0);
		Inside.Padding = IsBordered ? new Thickness(14, 12) : new Thickness(0, 14);
		// A card's parts are separated; a row's are one paragraph, so they sit closer together.
		Inside.Spacing = IsBordered ? 8 : 5;
	}

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

	/// <inheritdoc cref="AccentColourProperty"/>
	public string? AccentColour
	{
		get => (string?)GetValue(AccentColourProperty);
		set => SetValue(AccentColourProperty, value);
	}

	/// <inheritdoc cref="IsPinnedProperty"/>
	public bool IsPinned
	{
		get => (bool)GetValue(IsPinnedProperty);
		set => SetValue(IsPinnedProperty, value);
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
	{
		var card = (ItemCard)bindable;
		card.ActionMark.IsVisible = newValue is true;
		card.Edge();
	}

	/// <summary>
	/// The card's own edge says what the marks inside it say, because a dot nine pixels across is easy
	/// to miss on a page of cards. News first, then pinned: a pinned card with something on it is a
	/// card with something on it.
	/// </summary>
	private void Edge()
	{
		if (!IsBordered)
		{
			// Nothing to paint: a row's only line is the hairline above it, and colouring that would
			// mark the gap between two rows rather than either of them. The red dot before the name and
			// the pin beside it are what a row says instead - a card only needs the edge because its
			// marks sit inside a box that is competing with them. Nothing to undo either: the halo is
			// only ever set by Pulse, which this branch never reaches.
			this.AbortAnimation(NewsPulse);
			return;
		}

		// Taken off before anything is decided, not only on the way past a pinned card. A dynamic
		// resource stays registered against the property until it is removed and paints itself back on
		// every time the dictionary is read again - so a card that was pinned first and got its news
		// afterwards kept the accent edge, and the danger one set below never showed.
		Frame.RemoveDynamicResource(Border.StrokeProperty);

		if (HasUnseenAction)
		{
			Frame.SetAppTheme(Border.StrokeProperty, Look("DangerLight"), Look("DangerDark"));
			Pulse();
			return;
		}

		this.AbortAnimation(NewsPulse);
		Frame.Shadow = null;

		if (IsPinned)
		{
			Frame.SetDynamicResource(Border.StrokeProperty, "Accent");
			return;
		}

		Frame.SetAppTheme(Border.StrokeProperty, Look("CardStrokeLight"), Look("CardStrokeDark"));
	}

	private void PaintTheEdge(string? colour)
	{
		var known = !string.IsNullOrWhiteSpace(colour) && Color.TryParse(colour, out var parsed);
		AccentEdge.IsVisible = known;

		if (known)
		{
			AccentEdge.Color = Color.Parse(colour!);
		}
	}

	/// <summary>
	/// The edge breathes, which is what catches an eye that was elsewhere - app.css's card-with-news,
	/// as a halo that swells and fades rather than a box-shadow spread MAUI has no equivalent of. The
	/// colour is there whether or not anything moves; this is only what makes it noticed.
	///
	/// Called off where the reader has asked for less motion. An edge that never stops moving is
	/// exactly what somebody turns that setting on to be rid of - the same rule app.css writes under
	/// prefers-reduced-motion, asked of the platform here because there is no media query to read.
	/// </summary>
	private void Pulse()
	{
		this.AbortAnimation(NewsPulse);

		if (!MotionIsWanted)
		{
			Frame.Shadow = null;
			return;
		}

		var halo = new Shadow
		{
			Brush = Look(Application.Current?.RequestedTheme == AppTheme.Dark ? "DangerDark" : "DangerLight"),
			Offset = Point.Zero,
			Radius = 0,
			Opacity = 0
		};

		Frame.Shadow = halo;

		// Out and back rather than out and jump: a halo that snaps to nothing every 2.4 seconds reads
		// as a glitch rather than as breathing.
		var pulse = new Animation();
		pulse.Add(0, 0.5, new Animation(Swell, 0, 1, Easing.SinInOut));
		pulse.Add(0.5, 1, new Animation(Swell, 1, 0, Easing.SinInOut));
		pulse.Commit(this, NewsPulse, length: 2400, repeat: () => true);

		void Swell(double much)
		{
			halo.Radius = (float)(much * 8);
			halo.Opacity = (float)(much * 0.22);
		}
	}

	/// <summary>
	/// Whether the phone has been asked to animate at all. Android says so by scaling every animation
	/// to nothing, which is the setting a reader turns on for exactly this kind of thing.
	/// </summary>
	private static bool MotionIsWanted =>
#if ANDROID
		Android.Provider.Settings.Global.GetFloat(
			Android.App.Application.Context.ContentResolver,
			Android.Provider.Settings.Global.AnimatorDurationScale,
			1f) > 0f;
#else
		true;
#endif

	private static Brush Look(string key)
		=> Application.Current?.Resources.TryGetValue(key, out var value) is true && value is Color colour
			? new SolidColorBrush(colour)
			: Brush.Transparent;

	/// <summary>
	/// The footnote and the hairline above it go together: a card with nothing to say about itself
	/// must not end in a line drawn under nothing.
	/// </summary>
	private void Foot(View? extras)
	{
		Slot.Fill(ExtrasHost, extras);
		Footer.IsVisible = extras is not null;

		if (extras is not null)
		{
			Footer.SetBinding(IsVisibleProperty, static (View held) => held.IsVisible, source: extras);
		}
		else
		{
			Footer.RemoveBinding(IsVisibleProperty);
		}
	}

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
