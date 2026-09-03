namespace Orbit.Maui.Controls;

/// <summary>
/// Orbit's mark, at whatever size the caller asks for.
///
/// The drawing is Orbit.Web's, unit for unit: a 26-unit canvas, a ring 22 across and 11 tall tilted
/// about its centre, and a 6-unit body at that same centre. Asking for a different size scales the
/// whole canvas rather than resizing each shape, which is what an SVG's viewBox does and what keeps the
/// stroke in proportion with everything else.
/// </summary>
public partial class OrbitMark : ContentView
{
	/// <summary>The canvas the drawing is laid out on - Orbit.Web's viewBox, and its unit.</summary>
	private const double CanvasSize = 26.0;

	public static readonly BindableProperty MarkSizeProperty = BindableProperty.Create(
		nameof(MarkSize), typeof(double), typeof(OrbitMark), CanvasSize, propertyChanged: OnMarkSizeChanged);

	public OrbitMark()
	{
		InitializeComponent();
		Draw();
	}

	/// <summary>How wide the finished mark should be. The canvas is scaled to it.</summary>
	public double MarkSize
	{
		get => (double)GetValue(MarkSizeProperty);
		set => SetValue(MarkSizeProperty, value);
	}

	private static void OnMarkSizeChanged(BindableObject bindable, object oldValue, object newValue)
		=> ((OrbitMark)bindable).Draw();

	/// <summary>
	/// Scaling the canvas rather than the shapes on it. The control asks its parent for the finished
	/// size, because scaling is a render transform and layout would otherwise reserve the canvas's own
	/// 26 units however large the mark is drawn.
	/// </summary>
	private void Draw()
	{
		WidthRequest = MarkSize;
		HeightRequest = MarkSize;
		Canvas.Scale = MarkSize / CanvasSize;
	}
}
