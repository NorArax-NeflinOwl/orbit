using Microsoft.Extensions.DependencyInjection;
using Orbit.Core.Advertising;
using Orbit.Mobile.Advertising;
using Orbit.Mobile.Api;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens;

namespace Orbit.Maui.Controls;

/// <summary>
/// The advertising bar along the foot of a screen. Reads what to show for itself rather than taking it
/// from a view model, so a page can carry one without its view model having to know anything about
/// advertising - the same reason FeatureLocked takes its sentence as a property.
///
/// Every screen shows the same advert for as long as the app is open (<see cref="Slot"/> below), so
/// moving between sections does not shuffle it under the reader's eye.
///
/// Pressing it opens the page it names, in the browser: the adverts point at pages this app does not
/// have - the docs, the security page - so the address is the web client's, built from what the server
/// says its web client is (see HouseAdLink). A deployment that has not said, and a phone that could not
/// ask, both leave the bar exactly what it was until 2026-09-10 - something to read rather than press.
/// </summary>
public partial class AdBanner : ContentView
{
    /// <summary>
    /// Which advert this run of the app shows. Chosen once, statically, for the reason the web's own
    /// slot is chosen once per visit: a slot that re-picked whenever a page was drawn would change
    /// while somebody was reading it.
    /// </summary>
    private static readonly int Slot = Random.Shared.Next();

    private readonly HouseAd? _advert = HouseAds.ForSlotOnAPhone(Slot);

    /// <summary>
    /// Whether the keyboard is up, which this bar stands aside for - see <see cref="IsShown"/>. Held
    /// rather than asked for each time, so the bar can follow it as it changes.
    /// </summary>
    private readonly SoftKeyboard? _keyboard =
        IPlatformApplication.Current?.Services.GetService<SoftKeyboard>();

    public AdBanner()
    {
        InitializeComponent();

        if (_keyboard is not null)
        {
            _keyboard.PropertyChanged += (_, _) => OnPropertyChanged(nameof(IsShown));
        }
    }

    public bool HasAdvert => _advert is not null;

    /// <summary>
    /// Whether the bar is on screen: when there is an advert to show, and the keyboard is away.
    ///
    /// The bar sits across the foot of every screen, which is exactly where a keyboard opens - so
    /// writing anything on a phone meant reading half a form with an advert over the rest of it, and on
    /// the calendar the advert covered the date and time of the event being typed in. Reported
    /// 2026-09-20. It comes back the moment the keyboard is put away; nothing about which advert is
    /// shown changes, so the reader is not handed a different one for having typed something.
    /// </summary>
    public bool IsShown => HasAdvert && _keyboard?.IsUp != true;

    public string Title => Translated(_advert?.Title);

    public string Body => Translated(_advert?.Body);

    /// <summary>
    /// Asked at the press rather than when the bar is drawn: every screen carries one of these, and
    /// asking the server on each would be a request per screen for something nobody may ever press.
    /// A press that goes nowhere is silent - there is no failure here the reader could act on, and an
    /// advert is not worth interrupting them over.
    /// </summary>
    private async void OnTapped(object? sender, TappedEventArgs args)
    {
        try
        {
            if (IPlatformApplication.Current?.Services.GetService<PublicShareClient>() is not { } links)
            {
                return;
            }

            if (HouseAdLink.For(_advert, await links.WebAddressAsync()) is { } address)
            {
                await Launcher.Default.OpenAsync(address);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Nothing on screen: see the summary above.
            _ = exception;
        }
    }

    /// <summary>
    /// In the language the rest of the screen is in. Read through the running app's own Translations
    /// rather than a markup extension, because what is being translated is chosen at runtime - the
    /// extension only handles text written into the XAML.
    /// </summary>
    private static string Translated(string? text)
        => text is null
            ? string.Empty
            : IPlatformApplication.Current?.Services.GetService<Translations>() is { } translations
                ? translations[text]
                : text;
}
