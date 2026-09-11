using System.Globalization;
using Microsoft.Maui.Controls.Shapes;
using Orbit.Mobile.Localization;
using Path = Microsoft.Maui.Controls.Shapes.Path;

namespace Orbit.Maui.Controls;

/// <summary>
/// What a screen shows while it waits: Orbit's icon inside a turning ring - the phone's half of Orbit.Web's
/// Components/Loading.razor (.loading-orbit in app.css), in place of MAUI's own ActivityIndicator, so a
/// wait looks like the same thing on both clients.
///
/// Only the arc turns. The icon holds still in the middle of it: it says whose screen this is, and a logo
/// that spins is a logo nobody can read. The icon is the launcher's and the browser tab's own
/// (Resources/AppIcon, wwwroot/favicon.svg) in its own colours, so it looks the same in either theme; the
/// arc is the reader's accent over a track in the hairline colour, as the web's border-top-color over
/// var(--border).
///
/// Every shape is worked out from <see cref="RingSize"/> rather than drawn on a canvas and scaled: nothing
/// is drawn outside the control's own box, and the one thing that turns is a square view turning about
/// its own middle (see OrbitMark for what a transform about the wrong point did on Android).
///
/// One animation, and only while it can be seen: it runs while <see cref="IsRunning"/> is set, the control
/// is visible and it is on a window, and stops the moment any of the three is not. Where the reader has
/// asked for less motion (see <see cref="Motion"/>) it is slowed rather than stopped, as the web's is: a
/// ring that never moves reads as a screen that has stopped, which is the one thing it is there to deny.
/// Android's setting switches MAUI's animations off altogether, so the slowed turn is a step every quarter
/// of a second rather than an animation.
/// </summary>
public sealed class OrbitLoading : ContentView
{
	/// <summary>The web's ring: 64 across with a 34 icon inside it (.loading-orbit-ring, .loading-orbit-icon).</summary>
	private const double IconShare = 34.0 / 64.0;

	/// <summary>One turn, as .loading-orbit-spinner's 0.9s.</summary>
	private const uint TurnMilliseconds = 900;

	/// <summary>Twelve steps of a quarter second: three seconds a turn, the web's reduced-motion duration.</summary>
	private static readonly TimeSpan SlowedStep = TimeSpan.FromMilliseconds(250);

	private const double SlowedStepDegrees = 30;

	private const string TurnAnimation = "OrbitLoadingTurn";

	/// <summary>The icon's tile, as favicon.svg and the launcher icon paint it.</summary>
	private static readonly Color TileColour = Color.FromArgb("#512BD4");

	/// <summary>
	/// Whether something is being waited for. Also whether the control is shown at all, which is what
	/// ActivityIndicator's callers bound IsVisible to beside it - one binding here does both.
	/// </summary>
	public static readonly BindableProperty IsRunningProperty = BindableProperty.Create(
		nameof(IsRunning), typeof(bool), typeof(OrbitLoading), false,
		propertyChanged: (loading, _, _) => ((OrbitLoading)loading).ShowWhetherRunning());

	/// <summary>
	/// How far across the ring is. 64 is the web's for a wait that has a screen to itself; 30 its inline one
	/// (.loading-orbit-inline), for a wait in a corner of a screen that is already drawn.
	/// </summary>
	public static readonly BindableProperty RingSizeProperty = BindableProperty.Create(
		nameof(RingSize), typeof(double), typeof(OrbitLoading), 64d,
		propertyChanged: (loading, _, _) => ((OrbitLoading)loading).Draw());

	/// <summary>
	/// What a screen reader says, which has nothing to read in a turning ring. "Loading…" unless a screen
	/// has something more useful to say about what it is waiting for - the web's Says, kept for the ear.
	/// </summary>
	public static readonly BindableProperty DescriptionProperty = BindableProperty.Create(
		nameof(Description), typeof(string), typeof(OrbitLoading), null,
		propertyChanged: (loading, _, _) => ((OrbitLoading)loading).Describe());

	private readonly Grid _canvas = new() { HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
	private readonly Path _track = new() { Aspect = Stretch.None, Fill = Brush.Transparent };
	private readonly Path _arc = new() { Aspect = Stretch.None, Fill = Brush.Transparent, StrokeLineCap = PenLineCap.Round };
	private readonly Border _tile = new() { StrokeThickness = 0, Padding = 0, BackgroundColor = TileColour };
	private readonly Path _orbit = new() { Aspect = Stretch.None, Fill = Brush.Transparent, Stroke = Brush.White };
	private readonly Ellipse _body = new() { Fill = Brush.White };

	private bool _isTurning;
	private IDispatcherTimer? _slowTurn;

	public OrbitLoading()
	{
		// The track takes the hairline's colour in each theme; the arc follows the accent, which the reader
		// can change while a screen is open - so it is a dynamic resource rather than a colour read once.
		_track.SetAppTheme(Shape.StrokeProperty, Look("CardStrokeLight"), Look("CardStrokeDark"));
		_arc.SetDynamicResource(Shape.StrokeProperty, "Accent");

		foreach (View shape in new View[] { _track, _arc, _tile, _orbit, _body })
		{
			shape.HorizontalOptions = LayoutOptions.Center;
			shape.VerticalOptions = LayoutOptions.Center;
			shape.InputTransparent = true;
			_canvas.Add(shape);
		}

		Content = _canvas;
		IsVisible = false;
		Draw();
		Describe();

		Loaded += (_, _) => TurnWhileSeen();
		Unloaded += (_, _) => StopTurning();
		PropertyChanged += (_, changed) =>
		{
			if (changed.PropertyName == nameof(IsVisible))
			{
				TurnWhileSeen();
			}
		};
	}

	/// <inheritdoc cref="IsRunningProperty"/>
	public bool IsRunning
	{
		get => (bool)GetValue(IsRunningProperty);
		set => SetValue(IsRunningProperty, value);
	}

	/// <inheritdoc cref="RingSizeProperty"/>
	public double RingSize
	{
		get => (double)GetValue(RingSizeProperty);
		set => SetValue(RingSizeProperty, value);
	}

	/// <inheritdoc cref="DescriptionProperty"/>
	public string? Description
	{
		get => (string?)GetValue(DescriptionProperty);
		set => SetValue(DescriptionProperty, value);
	}

	private void ShowWhetherRunning()
	{
		IsVisible = IsRunning;
		TurnWhileSeen();
	}

	private void TurnWhileSeen()
	{
		if (IsRunning && IsVisible && IsLoaded)
		{
			StartTurning();
		}
		else
		{
			StopTurning();
		}
	}

	private void StartTurning()
	{
		if (_isTurning)
		{
			return;
		}

		_isTurning = true;

		if (Motion.IsWanted)
		{
			var turn = new Animation(degrees => _arc.Rotation = degrees, 0, 360, Easing.Linear);
			turn.Commit(this, TurnAnimation, length: TurnMilliseconds, repeat: () => _isTurning);
			return;
		}

		_slowTurn = Dispatcher.CreateTimer();
		_slowTurn.Interval = SlowedStep;
		_slowTurn.Tick += (_, _) => _arc.Rotation = (_arc.Rotation + SlowedStepDegrees) % 360;
		_slowTurn.Start();
	}

	private void StopTurning()
	{
		if (!_isTurning)
		{
			return;
		}

		_isTurning = false;
		this.AbortAnimation(TurnAnimation);
		_slowTurn?.Stop();
		_slowTurn = null;
	}

	/// <summary>
	/// Every shape from the one size, in the web's proportions: the ring's border is 3 on the page-sized
	/// ring and 2 on the inline one, and the icon is favicon.svg's own drawing - a tile with corners 14 of
	/// its 64, a path 50 by 25 tilted 28 degrees and 3.8 thick, and a body 14 across - at 34/64 of the ring.
	/// </summary>
	private void Draw()
	{
		var size = RingSize;
		var thickness = size >= 48 ? 3 : 2;
		var centre = size / 2;
		var radius = (size - thickness) / 2;

		_canvas.WidthRequest = size;
		_canvas.HeightRequest = size;

		foreach (var ring in new[] { _track, _arc })
		{
			ring.WidthRequest = size;
			ring.HeightRequest = size;
			ring.StrokeThickness = thickness;
		}

		// The whole circle, as two halves; and the quarter across the top that border-top-color paints.
		_track.Data = Geometry(
			$"M {N(centre - radius)},{N(centre)} A {N(radius)},{N(radius)} 0 1 1 {N(centre + radius)},{N(centre)} "
			+ $"A {N(radius)},{N(radius)} 0 1 1 {N(centre - radius)},{N(centre)} Z");
		var corner = radius * Math.Sqrt(0.5);
		_arc.Data = Geometry(
			$"M {N(centre - corner)},{N(centre - corner)} A {N(radius)},{N(radius)} 0 0 1 {N(centre + corner)},{N(centre - corner)}");

		var icon = Math.Round(size * IconShare);
		var unit = icon / 64;
		_tile.WidthRequest = icon;
		_tile.HeightRequest = icon;
		_tile.StrokeShape = new RoundRectangle { CornerRadius = 14 * unit };

		// The tilted path, as two arcs from one end of its long axis to the other and back - the tilt in the
		// arcs' own rotation, as OrbitMark draws it, rather than a rotated view.
		var middle = icon / 2;
		var (rx, ry) = (25 * unit, 12.5 * unit);
		var tilt = 28 * Math.PI / 180;
		var (dx, dy) = (rx * Math.Cos(tilt), rx * Math.Sin(tilt));
		_orbit.WidthRequest = icon;
		_orbit.HeightRequest = icon;
		_orbit.StrokeThickness = 3.8 * unit;
		_orbit.Data = Geometry(
			$"M {N(middle + dx)},{N(middle - dy)} A {N(rx)},{N(ry)} -28 0 1 {N(middle - dx)},{N(middle + dy)} "
			+ $"A {N(rx)},{N(ry)} -28 0 1 {N(middle + dx)},{N(middle - dy)} Z");

		_body.WidthRequest = 14 * unit;
		_body.HeightRequest = 14 * unit;
	}

	private void Describe()
	{
		var translations = IPlatformApplication.Current?.Services.GetService<Translations>();
		SemanticProperties.SetDescription(
			this, Description is { Length: > 0 } said ? said : translations?["Loading…"] ?? "Loading…");
	}

	private static Geometry Geometry(string data)
		=> (Geometry)new PathGeometryConverter().ConvertFromInvariantString(data)!;

	/// <summary>A coordinate as path data wants it, whatever the phone's own number format.</summary>
	private static string N(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

	private static Brush Look(string key)
		=> Application.Current?.Resources.TryGetValue(key, out var value) is true && value is Color colour
			? new SolidColorBrush(colour)
			: Brush.Transparent;
}
