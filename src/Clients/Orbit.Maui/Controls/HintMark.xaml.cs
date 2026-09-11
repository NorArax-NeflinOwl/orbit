using Microsoft.Maui.Controls.Shapes;
using Orbit.Mobile.Localization;

namespace Orbit.Maui.Controls;

/// <summary>
/// The mark a folded sentence sits behind - see the markup for why it is a control of its own. It only
/// draws the mark and says when it was pressed; where the sentence opens is up to whoever holds it.
/// </summary>
public partial class HintMark : ContentView
{
	/// <summary>
	/// Whether this is news rather than reference - see Orbit.Web's FieldHint, which draws the same
	/// distinction. A "!" in the warning colour for something true of the screen right now; a quiet "?"
	/// for what a thing is for, which most readers already know.
	/// </summary>
	public static readonly BindableProperty WarnsProperty =
		BindableProperty.Create(nameof(Warns), typeof(bool), typeof(HintMark), false,
			propertyChanged: (mark, _, _) => ((HintMark)mark).Redraw());

	/// <summary>The sentence the mark folds away, which a screen reader is given as the mark's hint.</summary>
	public static readonly BindableProperty TextProperty =
		BindableProperty.Create(nameof(Text), typeof(string), typeof(HintMark), string.Empty);

	public HintMark()
	{
		InitializeComponent();
		Redraw();
	}

	/// <summary>Raised on every press; the holder opens or folds the sentence.</summary>
	public event EventHandler? Pressed;

	public bool Warns
	{
		get => (bool)GetValue(WarnsProperty);
		set => SetValue(WarnsProperty, value);
	}

	public string Text
	{
		get => (string)GetValue(TextProperty);
		set => SetValue(TextProperty, value);
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

	private void OnPressed(object? sender, EventArgs e) => Pressed?.Invoke(this, EventArgs.Empty);

	/// <summary>
	/// Neither colour is written in the markup on purpose: a value set there is a *local* value, and in
	/// MAUI a local value beats a dynamic resource - so the theme could never get in. See CheckCircle,
	/// which says the same about its ring.
	/// </summary>
	private void Redraw()
	{
		Ring.ClearValue(Shape.StrokeProperty);
		Glyph.ClearValue(Label.TextColorProperty);

		// Named per theme rather than as one dynamic resource, the way CheckCircle picks its own: the
		// palette holds a light and a dark colour under two keys, not one that follows the theme.
		var light = Warns ? "AwayLight" : "TertiaryTextLight";
		var dark = Warns ? "AwayDark" : "TertiaryTextDark";
		Ring.SetAppTheme(Shape.StrokeProperty, Look(light), Look(dark));
		Glyph.SetAppTheme(Label.TextColorProperty, Look(light), Look(dark));
		Glyph.Text = Warns ? "!" : "?";

		OnPropertyChanged(nameof(AskedAs));
	}

	/// <inheritdoc cref="CheckCircle.Look"/>
	private static Color Look(string key)
		=> Application.Current?.Resources.TryGetValue(key, out var value) is true && value is Color colour
			? colour
			: Colors.Transparent;
}
