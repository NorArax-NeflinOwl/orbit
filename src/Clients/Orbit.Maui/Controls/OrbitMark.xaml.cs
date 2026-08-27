namespace Orbit.Maui.Controls;

/// <summary>
/// Orbit's mark, at whatever size the caller asks for.
///
/// The proportions are taken from Orbit.Web's sidebar logo, where the drawing sits on a 26-unit canvas:
/// a ring 22 across and 11 tall, tilted, with a 6-unit body at its centre. Kept as ratios so one
/// drawing serves a navigation bar and a sign-in screen without either being a second copy that can
/// drift.
/// </summary>
public partial class OrbitMark : ContentView
{
	private const double RingWidthRatio = 22.0 / 26.0;
	private const double RingHeightRatio = 11.0 / 26.0;
	private const double RingStrokeRatio = 1.6 / 26.0;
	private const double BodyRatio = 6.0 / 26.0;

	public static readonly BindableProperty MarkSizeProperty = BindableProperty.Create(
		nameof(MarkSize), typeof(double), typeof(OrbitMark), 26.0, propertyChanged: OnMarkSizeChanged);

	public OrbitMark()
	{
		InitializeComponent();
		Draw();
	}

	/// <summary>The width of the whole mark. Everything else is a fixed fraction of it.</summary>
	public double MarkSize
	{
		get => (double)GetValue(MarkSizeProperty);
		set => SetValue(MarkSizeProperty, value);
	}

	private static void OnMarkSizeChanged(BindableObject bindable, object oldValue, object newValue)
		=> ((OrbitMark)bindable).Draw();

	private void Draw()
	{
		Ring.WidthRequest = MarkSize * RingWidthRatio;
		Ring.HeightRequest = MarkSize * RingHeightRatio;
		Ring.StrokeThickness = MarkSize * RingStrokeRatio;
		Body.WidthRequest = MarkSize * BodyRatio;
		Body.HeightRequest = MarkSize * BodyRatio;
	}
}
