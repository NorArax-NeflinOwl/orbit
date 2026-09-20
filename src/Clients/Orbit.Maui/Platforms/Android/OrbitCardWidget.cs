using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Widget;
using Microsoft.EntityFrameworkCore;
using Orbit.Maui.Platform;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Widgets;

namespace Orbit.Maui;

/// <summary>
/// Orbit's third home screen widget: whichever of the dashboard's cards the reader picked when they
/// placed it - notes, task lists, what is coming up, a shelf, or the places they keep.
///
/// Asked for on 2026-09-20 as "any tile from the dashboard". Unlike the other two it is configured:
/// the launcher opens <see cref="CardWidgetConfigure"/> first and places the widget only if that
/// activity says yes, and the answer is kept against the widget's own id - several of these can sit on
/// one home screen, each showing a different card.
///
/// Everything else is the same as the other two, for the same reasons: nothing of the app is running
/// when it is drawn, so the database is read here; and nothing private is ever named on it. See
/// <see cref="CardAtAGlance"/>, which is the half that can be tested.
/// </summary>
[BroadcastReceiver(Label = "@string/orbit_card_widget_label", Exported = false)]
[IntentFilter(["android.appwidget.action.APPWIDGET_UPDATE", RefreshAction])]
[MetaData("android.appwidget.provider", Resource = "@xml/orbit_card_widget_info")]
public sealed class OrbitCardWidget : AppWidgetProvider
{
    /// <inheritdoc cref="OrbitTodayWidget"/>
    private const string LogTag = "OrbitWidget";

    /// <inheritdoc cref="OrbitCalendarWidget"/>
    private const string RefreshAction = "com.orbitmaui.android.REFRESH_CARD_WIDGET";

    /// <summary>
    /// Where the reader's answer is kept, one key per placed widget. In Preferences rather than in the
    /// database: it is a fact about a card on a home screen rather than about this account's things,
    /// and it has to be readable in a broadcast receiver with no app around it.
    /// </summary>
    private static string KeyFor(int widgetId) => $"widget.card.{widgetId}";

    /// <summary>What a widget shows, or null for one whose answer has been lost - see <see cref="Draw"/>.</summary>
    public static WidgetCard? ChosenFor(int widgetId)
        => Enum.TryParse<WidgetCard>(Preferences.Default.Get(KeyFor(widgetId), string.Empty), out var card)
            ? card
            : null;

    /// <summary>Keeps the reader's answer, which is what the configuration activity is for.</summary>
    public static void Remember(int widgetId, WidgetCard card)
        => Preferences.Default.Set(KeyFor(widgetId), card.ToString());

    /// <inheritdoc cref="OrbitTodayWidget.Refresh"/>
    public static void Refresh(Context context)
    {
        var refresh = new Intent(context, typeof(OrbitCardWidget));
        refresh.SetAction(RefreshAction);
        context.SendBroadcast(refresh);
    }

    public override void OnUpdate(Context? context, AppWidgetManager? appWidgetManager, int[]? appWidgetIds)
    {
        if (context is not null && appWidgetIds is not null)
        {
            Draw(context, appWidgetIds);
        }
    }

    public override void OnReceive(Context? context, Intent? intent)
    {
        base.OnReceive(context, intent);

        if (context is not null && intent?.Action == RefreshAction)
        {
            Draw(context, Placed(context));
        }
    }

    /// <summary>
    /// A widget taken off the home screen takes its answer with it. Without this, Preferences would
    /// collect a key per widget ever placed, and Android hands the ids out again.
    /// </summary>
    public override void OnDeleted(Context? context, int[]? appWidgetIds)
    {
        base.OnDeleted(context, appWidgetIds);

        foreach (var widgetId in appWidgetIds ?? [])
        {
            Preferences.Default.Remove(KeyFor(widgetId));
        }
    }

    private static int[] Placed(Context context)
        => AppWidgetManager.GetInstance(context)?.GetAppWidgetIds(
            new ComponentName(context, Java.Lang.Class.FromType(typeof(OrbitCardWidget)))) ?? [];

    /// <summary>
    /// One read for all of them, and a drawing per widget: two of these on a home screen showing two
    /// different cards are still one database to read.
    /// </summary>
    private void Draw(Context context, int[] widgetIds)
    {
        if (widgetIds.Length == 0)
        {
            return;
        }

        var manager = AppWidgetManager.GetInstance(context);
        var pending = GoAsync();

        _ = Task.Run(async () =>
        {
            try
            {
                var translations = new Translations(new PreferencesLanguageStore(Preferences.Default));
                var held = await ReadWhatIsHeldAsync();
                var now = DateTimeOffset.Now;

                foreach (var widgetId in widgetIds)
                {
                    var glance = (held, ChosenFor(widgetId)) switch
                    {
                        (null, _) => CardAtAGlance.ForNobodySignedIn(translations),
                        (_, null) => CardAtAGlance.NotChosenYet(translations),
                        ({ } what, { } card) => CardAtAGlance.Of(card, what, now, translations)
                    };

                    manager?.UpdateAppWidget(widgetId, Render(context, glance, widgetId));
                }
            }
            catch (Exception exception)
            {
                Android.Util.Log.Error(
                    LogTag, Java.Lang.Throwable.FromException(exception), "The card widget could not be drawn.");
            }
            finally
            {
                pending?.Finish();
            }
        });
    }

    /// <summary>
    /// Everything a card might need, or null when nobody is signed in - see TodayAtAGlance for why
    /// that is answered before anything is read rather than after.
    /// </summary>
    private static async Task<WhatThePhoneHolds?> ReadWhatIsHeldAsync()
    {
        try
        {
            if (await new SecureSessionStorage(SecureStorage.Default).ReadAsync() is null)
            {
                return null;
            }

            var options = new DbContextOptionsBuilder<OrbitLocalDbContext>()
                .UseSqlite(LocalDatabase.ConnectionString)
                .Options;

            await using var dbContext = new OrbitLocalDbContext(options);

            return new WhatThePhoneHolds(
                await dbContext.Notes.AsNoTracking().ToListAsync(),
                await dbContext.TaskLists.AsNoTracking().ToListAsync(),
                await dbContext.CalendarEvents.AsNoTracking().ToListAsync(),
                await dbContext.Inventories.AsNoTracking().ToListAsync(),
                await dbContext.Places.AsNoTracking().ToListAsync());
        }
        catch (Exception exception)
        {
            Android.Util.Log.Warn(
                LogTag, Java.Lang.Throwable.FromException(exception), "Nothing could be read for the card widget.");
            return null;
        }
    }

    private static RemoteViews Render(Context context, CardAtAGlance glance, int widgetId)
    {
        var views = new RemoteViews(context.PackageName, Resource.Layout.orbit_card_widget);
        views.SetTextViewText(Resource.Id.widgetDate, glance.Heading);
        views.RemoveAllViews(Resource.Id.widgetLines);

        for (var index = 0; index < glance.Lines.Count; index++)
        {
            views.AddView(Resource.Id.widgetLines, RenderLine(context, glance.Lines[index], widgetId, index));
        }

        views.SetTextViewText(Resource.Id.widgetMessage, glance.Message);
        views.SetViewVisibility(
            Resource.Id.widgetMessage,
            glance.Message.Length == 0 ? Android.Views.ViewStates.Gone : Android.Views.ViewStates.Visible);

        views.SetOnClickPendingIntent(Resource.Id.widgetRoot, Tap(context, glance.Url, Request(widgetId, 0)));
        return views;
    }

    private static RemoteViews RenderLine(Context context, CardLine line, int widgetId, int index)
    {
        var views = new RemoteViews(context.PackageName, Resource.Layout.orbit_card_widget_line);
        views.SetTextViewText(Resource.Id.widgetLineWhat, line.What);
        views.SetTextViewText(Resource.Id.widgetLineDetail, line.Detail);
        views.SetOnClickPendingIntent(Resource.Id.widgetLine, Tap(context, line.Url, Request(widgetId, index + 1)));
        return views;
    }

    /// <summary>
    /// A request code of its own per row <b>and per widget</b>: two PendingIntents that differ only in
    /// their extras are the same intent to Android, so without this the second widget's rows would open
    /// whatever the first widget's rows pointed at - see OrbitTodayWidget, which has half of this
    /// problem and only rows to tell apart.
    /// </summary>
    private static int Request(int widgetId, int index) => (widgetId * 16) + index;

    /// <inheritdoc cref="OrbitTodayWidget"/>
    private static PendingIntent? Tap(Context context, string url, int requestCode)
    {
        var intent = new Intent(context, typeof(MainActivity));
        intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop);
        if (url.Length > 0)
        {
            intent.PutExtra("url", url);
        }

        return PendingIntent.GetActivity(
            context, requestCode, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
    }
}
