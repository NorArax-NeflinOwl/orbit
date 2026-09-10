using Microsoft.Maui.Controls.Shapes;
using Orbit.Mobile.Localization;

namespace Orbit.Maui.Controls;

/// <summary>
/// A field's name with what it is for folded away behind a mark beside it - see the markup for why the
/// phone opens it in place rather than over the page.
///
/// It carries the name as well as the sentence, rather than sitting next to a Label somebody else drew.
/// That is what lets the two lay out as one thing: the mark on the name's line, the sentence under both,
/// and nothing at all in between while it is folded.
/// </summary>
public partial class FieldHint : ContentView
{
	public static readonly BindableProperty LabelProperty =
		BindableProperty.Create(nameof(Label), typeof(string), typeof(FieldHint), string.Empty);

	/// <summary>The sentence that used to sit under the field.</summary>
	public static readonly BindableProperty TextProperty =
		BindableProperty.Create(nameof(Text), typeof(string), typeof(FieldHint), string.Empty);

	/// <summary>
	/// Whether this is news rather than reference - see Orbit.Web's FieldHint, which draws the same
	/// distinction. A "!" in the warning colour for something true of the screen right now; a quiet "?"
	/// for what a field is for, which most readers already know.
	/// </summary>
	public static readonly BindableProperty WarnsProperty =
		BindableProperty.Create(nameof(Warns), typeof(bool), typeof(FieldHint), false,
			propertyChanged: (hint, _, _) => ((FieldHint)hint).Redraw());

	/// <summary>
	/// Whether the name is a section's heading rather than a field's label. The two are the same control
	/// because the fold is the same act; only the weight of the word above it differs.
	/// </summary>
	public static readonly BindableProperty IsHeadingProperty =
		BindableProperty.Create(nameof(IsHeading), typeof(bool), typeof(FieldHint), false,
			propertyChanged: (hint, _, _) => ((FieldHint)hint).Redraw());

	public FieldHint()
	{
		InitializeComponent();
		Redraw();
	}

	public string Label
	{
		get => (string)GetValue(LabelProperty);
		set => SetValue(LabelProperty, value);
	}

	public string Text
	{
		get => (string)GetValue(TextProperty);
		set => SetValue(TextProperty, value);
	}

	public bool Warns
	{
		get => (bool)GetValue(WarnsProperty);
		set => SetValue(WarnsProperty, value);
	}

	public bool IsHeading
	{
		get => (bool)GetValue(IsHeadingProperty);
		set => SetValue(IsHeadingProperty, value);
	}

	/// <summary>
	/// What the mark is called to a screen reader. Read from the app's own translations rather than
	/// passed in: it is the same two words on every screen, and asking each page to supply them is how
	/// one of them ends up in English.
	/// </summary>
	public string AskedAs
		=> IPlatformApplication.Current?.Services.GetService<Translations>() is { } translations
			? translations[Warns ? "What to know about this" : "What this is for"]
			: "What this is for";

	private void OnPressed(object? sender, EventArgs e) => Sentence.IsVisible = !Sentence.IsVisible;

	/// <summary>
	/// Neither colour is written in the markup on purpose: a value set there is a *local* value, and in
	/// MAUI a local value beats a dynamic resource - so the theme could never get in. See CheckCircle,
	/// which says the same about its ring.
	/// </summary>
	private void Redraw()
	{
		Ring.ClearValue(Shape.StrokeProperty);
		Mark.ClearValue(Microsoft.Maui.Controls.Label.TextColorProperty);
		NameLabel.ClearValue(StyleProperty);

		// Named per theme rather than as one dynamic resource, the way CheckCircle picks its own: the
		// palette holds a light and a dark colour under two keys, not one that follows the theme.
		var light = Warns ? "AwayLight" : "TertiaryTextLight";
		var dark = Warns ? "AwayDark" : "TertiaryTextDark";
		Ring.SetAppTheme(Shape.StrokeProperty, Look(light), Look(dark));
		Mark.SetAppTheme(Microsoft.Maui.Controls.Label.TextColorProperty, Look(light), Look(dark));
		Mark.Text = Warns ? "!" : "?";

		if (Application.Current?.Resources.TryGetValue(IsHeading ? "SectionHeading" : "FieldLabel", out var style) is true
			&& style is Style named)
		{
			NameLabel.Style = named;
		}

		OnPropertyChanged(nameof(AskedAs));
	}

	/// <inheritdoc cref="CheckCircle.Look"/>
	private static Color Look(string key)
		=> Application.Current?.Resources.TryGetValue(key, out var value) is true && value is Color colour
			? colour
			: Colors.Transparent;
}
