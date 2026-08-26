using System.Collections.Specialized;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using Orbit.Mobile.Screens.Location;
using MapPoint = Orbit.Mobile.Screens.Location.MapPoint;
using SensorLocation = Microsoft.Maui.Devices.Sensors.Location;

namespace Orbit.Maui.Features.Location;

public partial class MapPage : ContentPage
{
	/// <summary>
	/// How much ground the map shows when it first has something to point at. Wide enough to place a
	/// position in its neighbourhood, close enough that a street is still legible.
	/// </summary>
	private static readonly Distance InitialRadius = Distance.FromKilometers(2);

	private readonly MapViewModel _viewModel;

	/// <summary>Set once the map has been pointed somewhere, so later readings do not yank it back.</summary>
	private bool _hasBeenCentred;

	public MapPage(MapViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_viewModel.Points.CollectionChanged += OnPointsChanged;
	}

	/// <summary>Typed so the list rows' bindings back up to the page can be compiled.</summary>
	public MapViewModel ViewModel => _viewModel;

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
	}

	/// <summary>
	/// The page outlives none of this, but the view model is resolved per screen and the handler would
	/// otherwise keep this page alive behind it.
	/// </summary>
	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_viewModel.Points.CollectionChanged -= OnPointsChanged;
	}

	private void OnPointsChanged(object? sender, NotifyCollectionChangedEventArgs e) => ShowPins();

	private void ShowPins()
	{
		PositionsMap.Pins.Clear();
		foreach (var point in _viewModel.Points)
		{
			PositionsMap.Pins.Add(new Pin
			{
				Label = point.Label,
				Address = point.Description,
				Location = new SensorLocation(point.Latitude, point.Longitude)
			});
		}

		// Centred on the first point, which is the reader's own whenever they have one - see
		// MapViewModel.ShowPointsOnMap for the order.
		if (!_hasBeenCentred && _viewModel.Points.FirstOrDefault() is { } first)
		{
			PositionsMap.MoveToRegion(MapSpan.FromCenterAndRadius(
				new SensorLocation(first.Latitude, first.Longitude), InitialRadius));
			_hasBeenCentred = true;
		}
	}
}
