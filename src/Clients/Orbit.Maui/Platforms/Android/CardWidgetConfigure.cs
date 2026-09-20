using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.OS;
using Orbit.Maui.Platform;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Widgets;

namespace Orbit.Maui;

/// <summary>
/// Which card a placed <see cref="OrbitCardWidget"/> shows, asked the moment it is placed.
///
/// The launcher opens this before the widget exists on the home screen and places it only if this
/// answers <see cref="Result.Ok"/> - so backing out of the question leaves nothing behind, which is
/// why the cancelled result is set first and the good one only after an answer.
///
/// A list in the platform's own dialog rather than a MAUI page: this runs before Orbit has started,
/// often with no app process at all, and a MAUI page would mean building the whole application to ask
/// one question. The same reason the widgets themselves read the database directly.
///
/// Named in full - the name a .NET for Android type gets in the manifest is otherwise a hash, and the
/// widget's own XML has to name this class to point the launcher at it.
/// </summary>
[Activity(
    Name = "com.orbitmaui.android.CardWidgetConfigure",
    Label = "@string/orbit_card_widget_label",
    Theme = "@android:style/Theme.Translucent.NoTitleBar",
    Exported = true)]
[IntentFilter(["android.appwidget.action.APPWIDGET_CONFIGURE"])]
public sealed class CardWidgetConfigure : Activity
{
    /// <summary>The order the choices are offered in, which is the dashboard's own order.</summary>
    private static readonly WidgetCard[] Cards =
    [
        WidgetCard.Notes, WidgetCard.Tasks, WidgetCard.Upcoming, WidgetCard.Inventory, WidgetCard.Places
    ];

    private int _widgetId = AppWidgetManager.InvalidAppwidgetId;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        _widgetId = Intent?.GetIntExtra(
            AppWidgetManager.ExtraAppwidgetId, AppWidgetManager.InvalidAppwidgetId)
            ?? AppWidgetManager.InvalidAppwidgetId;

        // Before anything else: whatever happens from here - the reader pressing back, the dialog being
        // dismissed, this activity being killed - must leave the home screen as it was.
        SetResult(Result.Canceled, Answer());

        if (_widgetId == AppWidgetManager.InvalidAppwidgetId)
        {
            Finish();
            return;
        }

        Ask();
    }

    private void Ask()
    {
        var translations = new Translations(new PreferencesLanguageStore(Preferences.Default));

        // Given the platform's own dialog style rather than the one a translucent activity would hand
        // it, which is the pre-Material look - a black bar over a white list. DeviceDefault follows the
        // phone's own light or dark, which is the same thing the widgets themselves follow.
        var dialog = new AlertDialog.Builder(this, Android.Resource.Style.ThemeDeviceDefaultDialogAlert)
            .SetTitle(translations["What should this show?"])!
            .SetItems(
                [.. Cards.Select(card => CardAtAGlance.HeadingOf(card, translations))],
                (_, chosen) => Chose(Cards[chosen.Which]))!
            .SetOnCancelListener(new Dismissed(this))!
            .Create();

        dialog?.Show();
    }

    private void Chose(WidgetCard card)
    {
        OrbitCardWidget.Remember(_widgetId, card);

        // Drawn before the launcher is told yes: Android's own update for a newly placed widget can
        // arrive before the answer is kept, and the card would then draw as one nobody has answered for.
        OrbitCardWidget.Refresh(this);

        SetResult(Result.Ok, Answer());
        Finish();
    }

    /// <summary>
    /// The widget this was asked about, which every answer has to carry back - the launcher matches the
    /// result to the widget by it.
    /// </summary>
    private Intent Answer() => new Intent().PutExtra(AppWidgetManager.ExtraAppwidgetId, _widgetId);

    /// <summary>Pressing back or tapping outside the dialog: the cancelled result already set stands.</summary>
    private sealed class Dismissed(Activity activity) : Java.Lang.Object, IDialogInterfaceOnCancelListener
    {
        public void OnCancel(IDialogInterface? dialog) => activity.Finish();
    }
}
