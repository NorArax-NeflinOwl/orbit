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

	/// <inheritdoc cref="HintMark.WarnsProperty"/>
	public static readonly BindableProperty WarnsProperty =
		BindableProperty.Create(nameof(Warns), typeof(bool), typeof(FieldHint), false);

	/// <summary>
	/// Whether the name is a section's heading rather than a field's label. The two are the same control
	/// because the fold is the same act; only the weight of the word above it differs.
	/// </summary>
	public static readonly BindableProperty IsHeadingProperty =
		BindableProperty.Create(nameof(IsHeading), typeof(bool), typeof(FieldHint), false,
			propertyChanged: (hint, _, _) => ((FieldHint)hint).Redraw());

	/// <summary>
	/// The look of the name where it is neither a section's heading nor a field's label - the one heading
	/// a screen draws for itself because there is no bar above it to carry its name (sign-in's
	/// neighbours). Wins over <see cref="IsHeading"/> when set.
	/// </summary>
	public static readonly BindableProperty LabelStyleProperty =
		BindableProperty.Create(nameof(LabelStyle), typeof(Style), typeof(FieldHint), null,
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

	public Style? LabelStyle
	{
		get => (Style?)GetValue(LabelStyleProperty);
		set => SetValue(LabelStyleProperty, value);
	}

	private void OnPressed(object? sender, EventArgs e) => Sentence.IsVisible = !Sentence.IsVisible;

	/// <summary>
	/// A hint centred on its screen keeps its name and its sentence centred too. Without this the name
	/// jumps to the left edge the moment the sentence opens: the sentence widens the control to the whole
	/// line, and the row holding the name lays out from its start.
	/// </summary>
	protected override void OnPropertyChanged(string? propertyName = null)
	{
		base.OnPropertyChanged(propertyName);

		// NameRow is null while the base constructor runs, before InitializeComponent has built it.
		if (propertyName != HorizontalOptionsProperty.PropertyName || NameRow is null)
		{
			return;
		}

		var centred = HorizontalOptions.Alignment == LayoutAlignment.Center;
		NameRow.HorizontalOptions = centred ? LayoutOptions.Center : LayoutOptions.Start;
		Sentence.HorizontalTextAlignment = centred ? TextAlignment.Center : TextAlignment.Start;
	}

	/// <summary>
	/// The name's style is picked here rather than in the markup, for the reason HintMark gives about its
	/// colours: a value written there is a local value, and it would beat the style a page sets later.
	/// </summary>
	private void Redraw()
	{
		NameLabel.ClearValue(StyleProperty);

		if (LabelStyle is { } own)
		{
			NameLabel.Style = own;
			return;
		}

		if (Application.Current?.Resources.TryGetValue(IsHeading ? "SectionHeading" : "FieldLabel", out var style) is true
			&& style is Style named)
		{
			NameLabel.Style = named;
		}
	}
}
