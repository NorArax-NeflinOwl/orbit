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
/// Orbit's home screen widget: the day, and the few things still ahead in it.
///
/// The awkward part of a widget is that none of the app is running when it is drawn. The launcher asks
/// for it in a broadcast, which can arrive with no MAUI application, no service container and no signed
/// in session in memory - so everything this needs is built here, from the two things that survive the
/// app being closed: the secure store and the database file.
///
/// It reads the database rather than a snapshot the app leaves behind, because "today" is a question
/// with a different answer every midnight and a snapshot taken at nine in the evening is wrong by
/// morning. Reading it costs two queries against a local file, which is what the half-hourly redraw
/// Android offers is for.
///
/// What is shown, and what is deliberately not, is <see cref="TodayAtAGlance"/>'s to decide - it is the
/// half that can be tested. This class is the drawing.
/// </summary>
[BroadcastReceiver(Label = "Orbit", Exported = false)]
[IntentFilter(["android.appwidget.action.APPWIDGET_UPDATE", RefreshAction])]
[MetaData("android.appwidget.provider", Resource = "@xml/orbit_today_widget_info")]
public sealed class OrbitTodayWidget : AppWidgetProvider
{
    /// <summary>What this writes to the device log under - there is no screen for it to report on.</summary>
    private const string LogTag = "OrbitWidget";

    /// <summary>
    /// Orbit's own way of asking for a redraw. Android's APPWIDGET_UPDATE cannot be used for it: it is a
    /// protected broadcast, which only the system may send - an app that tries is refused outright.
    /// </summary>
    private const string RefreshAction = "com.orbitmaui.android.REFRESH_TODAY_WIDGET";

    /// <summary>
    /// Asks every placed widget to redraw. Called when the app is put down, which is both the moment its
    /// data is as fresh as it is going to get and the moment the reader is on their way back to the home
    /// screen - see App.CreateWindow.
    ///
    /// Sent to this receiver rather than drawn where it is called from, so the redraw runs on a
    /// receiver's footing: a process Android keeps alive for as long as the read takes, whether or not
    /// the app that asked for it is still around a moment later.
    /// </summary>
    public static void Refresh(Context context)
    {
        var refresh = new Intent(context, typeof(OrbitTodayWidget));
        refresh.SetAction(RefreshAction);
        context.SendBroadcast(refresh);
    }

    public override void OnUpdate(Context context, AppWidgetManager appWidgetManager, int[] appWidgetIds)
        => Draw(context, appWidgetIds);

    /// <summary>
    /// Orbit's own refresh, which names no widget - it is asked for when something changed, not about a
    /// particular card - so it redraws all of them.
    /// </summary>
    public override void OnReceive(Context context, Intent? intent)
    {
        base.OnReceive(context, intent);

        if (intent?.Action == RefreshAction)
        {
            Draw(context, Placed(context));
        }
    }

    private static int[] Placed(Context context)
        => AppWidgetManager.GetInstance(context)?.GetAppWidgetIds(
            new ComponentName(context, Java.Lang.Class.FromType(typeof(OrbitTodayWidget)))) ?? [];

    /// <summary>
    /// Through GoAsync, because a broadcast receiver is dead the moment this method returns and the read
    /// is asynchronous. Without it the widget draws from whatever the query had managed to produce,
    /// which is nothing.
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
                var glance = await ReadTodayAsync();
                foreach (var widgetId in widgetIds)
                {
                    manager?.UpdateAppWidget(widgetId, Render(context, glance));
                }
            }
            catch (Exception exception)
            {
                // Said out loud rather than swallowed. A widget that fails to draw leaves whatever was on
                // it last, or the empty initial layout - there is no screen for an error to appear on, so
                // without this the only symptom is a blank card and no way to tell why.
                Android.Util.Log.Error(LogTag, Java.Lang.Throwable.FromException(exception), "The widget could not be drawn.");
            }
            finally
            {
                pending.Finish();
            }
        });
    }

    /// <summary>
    /// The same two tables the calendar reads, and nothing else. Read outside the app's own
    /// repositories on purpose: those decrypt private content, and a widget must never show any - see
    /// TodayAtAGlance.
    /// </summary>
    private static async Task<TodayAtAGlance> ReadTodayAsync()
    {
        var translations = new Translations(new PreferencesLanguageStore(Preferences.Default));

        try
        {
            if (await new SecureSessionStorage(SecureStorage.Default).ReadAsync() is null)
            {
                return TodayAtAGlance.ForNobodySignedIn(translations);
            }

            var options = new DbContextOptionsBuilder<OrbitLocalDbContext>()
                .UseSqlite(LocalDatabase.ConnectionString)
                .Options;

            await using var dbContext = new OrbitLocalDbContext(options);

            return TodayAtAGlance.Of(
                await dbContext.TaskLists.AsNoTracking().ToListAsync(),
                await dbContext.CalendarEvents.AsNoTracking().ToListAsync(),
                DateTimeOffset.Now,
                translations);
        }
        catch (Exception exception)
        {
            // A widget that throws is one Android replaces with "Problem loading widget" until somebody
            // takes it off the home screen and puts it back. Every way this fails - no database yet, a
            // secure store that cannot be opened before first unlock, a schema a newer build wrote -
            // means the same thing to the reader, and Orbit itself is where they find out more. It is
            // still logged: the reader has nowhere to see why, so somebody looking into it needs one.
            Android.Util.Log.Warn(LogTag, Java.Lang.Throwable.FromException(exception), "Nothing could be read for the widget.");
            return TodayAtAGlance.ForNobodySignedIn(translations);
        }
    }

    private static RemoteViews Render(Context context, TodayAtAGlance glance)
    {
        var views = new RemoteViews(context.PackageName, Resource.Layout.orbit_today_widget);
        views.SetTextViewText(Resource.Id.widgetDate, glance.Date);
        views.RemoveAllViews(Resource.Id.widgetLines);

        for (var index = 0; index < glance.Lines.Count; index++)
        {
            views.AddView(Resource.Id.widgetLines, RenderLine(context, glance.Lines[index], index));
        }

        Say(views, Resource.Id.widgetMessage, glance.Message);
        Say(views, Resource.Id.widgetMore, glance.More);

        // Anywhere else on the widget opens Orbit where it would have opened anyway.
        views.SetOnClickPendingIntent(Resource.Id.widgetRoot, Tap(context, string.Empty, widgetIdForRequest: 0));
        return views;
    }

    private static RemoteViews RenderLine(Context context, GlanceLine line, int index)
    {
        var views = new RemoteViews(context.PackageName, Resource.Layout.orbit_today_widget_line);
        views.SetTextViewText(Resource.Id.widgetLineWhen, line.When);
        views.SetTextViewText(Resource.Id.widgetLineWhat, line.What);

        // One intent per line, each with its own request code: two PendingIntents that differ only in
        // their extras are the same intent to Android, and every line would open whatever the first one
        // pointed at.
        views.SetOnClickPendingIntent(Resource.Id.widgetLine, Tap(context, line.Url, index + 1));
        return views;
    }

    /// <summary>Hidden when there is nothing to say, rather than left as an empty line taking up room.</summary>
    private static void Say(RemoteViews views, int viewId, string text)
    {
        views.SetTextViewText(viewId, text);
        views.SetViewVisibility(viewId, text.Length == 0 ? Android.Views.ViewStates.Gone : Android.Views.ViewStates.Visible);
    }

    /// <summary>
    /// Opening Orbit at <paramref name="url"/> - the same paths a tapped notification travels, read by
    /// NotificationDestination and put on the intent under the name MainActivity already reads them
    /// from. A line with no url of its own simply starts the app.
    /// </summary>
    private static PendingIntent? Tap(Context context, string url, int widgetIdForRequest)
    {
        var intent = new Intent(context, typeof(MainActivity));
        intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop);
        if (url.Length > 0)
        {
            intent.PutExtra("url", url);
        }

        return PendingIntent.GetActivity(
            context, widgetIdForRequest, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
    }
}
