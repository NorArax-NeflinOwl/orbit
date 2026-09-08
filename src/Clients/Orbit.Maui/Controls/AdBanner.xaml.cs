using Orbit.Core.Advertising;
using Orbit.Mobile.Localization;

namespace Orbit.Maui.Controls;

/// <summary>
/// The advertising bar along the foot of a screen. Reads what to show for itself rather than taking it
/// from a view model, so a page can carry one without its view model having to know anything about
/// advertising - the same reason FeatureLocked takes its sentence as a property.
///
/// Every screen shows the same advert for as long as the app is open (<see cref="Slot"/> below), so
/// moving between sections does not shuffle it under the reader's eye.
///
/// It is not something to tap. The adverts point at pages this app does not have - the docs, the
/// security page - and the app is told the API's address but never the web client's, so there is
/// nowhere to send anybody. See info/future-plan.md.
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

    public AdBanner()
    {
        InitializeComponent();
    }

    public bool HasAdvert => _advert is not null;

    public string Title => Translated(_advert?.Title);

    public string Body => Translated(_advert?.Body);

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
