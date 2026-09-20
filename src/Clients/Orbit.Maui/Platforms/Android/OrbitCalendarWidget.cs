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
/// Orbit's second home screen widget: the month, with a dot on every day something wants.
///
/// Asked for on 2026-09-20 beside the one that was already there. <see cref="OrbitTodayWidget"/>
/// answers "what is still ahead today"; this answers "what does this month look like", which is the
/// question somebody glancing at a calendar on a home screen is actually asking.
///
/// It is drawn under the same two constraints that one is, for the same reasons: none of the app is
/// running when the launcher asks for it, so everything is built here from the secure store and the
/// database file; and nothing is named on it at all - what is on a day stays inside Orbit, and the
/// home screen gets a dot. See <see cref="MonthAtAGlance"/>, which is the half that can be tested;
/// this class is the drawing.
/// </summary>
[BroadcastReceiver(Label = "@string/orbit_calendar_widget_label", Exported = false)]
[IntentFilter(["android.appwidget.action.APPWIDGET_UPDATE", RefreshAction])]
[MetaData("android.appwidget.provider", Resource = "@xml/orbit_calendar_widget_info")]
public sealed class OrbitCalendarWidget : AppWidgetProvider
{
    /// <inheritdoc cref="OrbitTodayWidget"/>
    private const string LogTag = "OrbitWidget";

    /// <summary>
    /// Orbit's own way of asking for a redraw - its own action, not the other widget's: a broadcast
    /// names one receiver, and Android's APPWIDGET_UPDATE is protected and cannot be sent by an app.
    /// </summary>
    private const string RefreshAction = "com.orbitmaui.android.REFRESH_CALENDAR_WIDGET";

    /// <inheritdoc cref="OrbitTodayWidget.Refresh"/>
    public static void Refresh(Context context)
    {
        var refresh = new Intent(context, typeof(OrbitCalendarWidget));
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

    private static int[] Placed(Context context)
        => AppWidgetManager.GetInstance(context)?.GetAppWidgetIds(
            new ComponentName(context, Java.Lang.Class.FromType(typeof(OrbitCalendarWidget)))) ?? [];

    /// <inheritdoc cref="OrbitTodayWidget"/>
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
                var month = await ReadTheMonthAsync();
                foreach (var widgetId in widgetIds)
                {
                    manager?.UpdateAppWidget(widgetId, Render(context, month));
                }
            }
            catch (Exception exception)
            {
                Android.Util.Log.Error(
                    LogTag, Java.Lang.Throwable.FromException(exception), "The calendar widget could not be drawn.");
            }
            finally
            {
                pending?.Finish();
            }
        });
    }

    /// <summary>
    /// The same two tables the other widget reads, outside the app's own repositories for the same
    /// reason: those decrypt private content, and no widget may show any - see MonthAtAGlance.
    /// </summary>
    private static async Task<MonthAtAGlance> ReadTheMonthAsync()
    {
        var translations = new Translations(new PreferencesLanguageStore(Preferences.Default));

        try
        {
            if (await new SecureSessionStorage(SecureStorage.Default).ReadAsync() is null)
            {
                return MonthAtAGlance.ForNobodySignedIn(translations);
            }

            var options = new DbContextOptionsBuilder<OrbitLocalDbContext>()
                .UseSqlite(LocalDatabase.ConnectionString)
                .Options;

            await using var dbContext = new OrbitLocalDbContext(options);

            return MonthAtAGlance.Of(
                await dbContext.TaskLists.AsNoTracking().ToListAsync(),
                await dbContext.CalendarEvents.AsNoTracking().ToListAsync(),
                DateTimeOffset.Now,
                translations);
        }
        catch (Exception exception)
        {
            // Every way this fails means the same thing to the reader - see OrbitTodayWidget, which says
            // it at length. Logged all the same: there is no screen for them to see why.
            Android.Util.Log.Warn(
                LogTag, Java.Lang.Throwable.FromException(exception), "Nothing could be read for the calendar widget.");
            return MonthAtAGlance.ForNobodySignedIn(translations);
        }
    }

    private static RemoteViews Render(Context context, MonthAtAGlance month)
    {
        var views = new RemoteViews(context.PackageName, Resource.Layout.orbit_calendar_widget);
        views.SetTextViewText(Resource.Id.widgetMonth, month.Month);

        views.RemoveAllViews(Resource.Id.widgetWeekdays);
        foreach (var initial in month.Weekdays)
        {
            views.AddView(Resource.Id.widgetWeekdays, RenderInitial(context, initial));
        }

        views.RemoveAllViews(Resource.Id.widgetMonthGrid);
        foreach (var week in month.InWeeks())
        {
            views.AddView(Resource.Id.widgetMonthGrid, RenderWeek(context, week));
        }

        views.SetTextViewText(Resource.Id.widgetMonthMessage, month.Message);
        views.SetViewVisibility(
            Resource.Id.widgetMonthMessage,
            month.Message.Length == 0 ? Android.Views.ViewStates.Gone : Android.Views.ViewStates.Visible);

        // The whole card opens the calendar - one destination, because no square on it names anything
        // to open. The path is the one a tapped notification travels; see NotificationDestination.
        views.SetOnClickPendingIntent(Resource.Id.widgetRoot, Tap(context, "/calendar"));
        return views;
    }

    private static RemoteViews RenderWeek(Context context, IReadOnlyList<GlanceDay> week)
    {
        var views = new RemoteViews(context.PackageName, Resource.Layout.orbit_calendar_widget_week);
        foreach (var day in week)
        {
            views.AddView(Resource.Id.widgetWeek, RenderDay(context, day));
        }

        return views;
    }

    private static RemoteViews RenderDay(Context context, GlanceDay day)
    {
        var views = new RemoteViews(context.PackageName, Resource.Layout.orbit_calendar_widget_day);
        views.SetTextViewText(Resource.Id.widgetDayNumber, day.Number);
        views.SetTextColor(
            Resource.Id.widgetDayNumber,
            Colour(context, day.IsInTheMonth ? Resource.Color.orbitWidgetText : Resource.Color.orbitWidgetSubtleText));

        if (day.IsToday)
        {
            // Through SetInt rather than a dedicated call: setBackgroundResource is one of the methods
            // RemoteViews will forward by name, and there is no SetBackground of its own.
            views.SetInt(Resource.Id.widgetDayNumber, "setBackgroundResource", Resource.Drawable.orbit_widget_today);
        }

        // Invisible rather than gone: a square with no dot keeps the height of one with a dot, so the
        // weeks stay level.
        views.SetViewVisibility(
            Resource.Id.widgetDayDot,
            day.HasSomething ? Android.Views.ViewStates.Visible : Android.Views.ViewStates.Invisible);
        return views;
    }

    /// <summary>One of the seven initials: the day square with nothing marked on it.</summary>
    private static RemoteViews RenderInitial(Context context, string initial)
    {
        var views = new RemoteViews(context.PackageName, Resource.Layout.orbit_calendar_widget_day);
        views.SetTextViewText(Resource.Id.widgetDayNumber, initial);
        views.SetTextColor(Resource.Id.widgetDayNumber, Colour(context, Resource.Color.orbitWidgetSubtleText));
        views.SetViewVisibility(Resource.Id.widgetDayDot, Android.Views.ViewStates.Gone);
        return views;
    }

    /// <summary>
    /// A colour resource as the integer RemoteViews wants. Read against the context's own theme, which
    /// is how the night half of the widget's colours is picked up - see values-night/colors.xml.
    /// </summary>
    private static Android.Graphics.Color Colour(Context context, int colourResource)
        => new(context.Resources!.GetColor(colourResource, context.Theme));

    /// <inheritdoc cref="OrbitTodayWidget"/>
    private static PendingIntent? Tap(Context context, string url)
    {
        var intent = new Intent(context, typeof(MainActivity));
        intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop);
        intent.PutExtra("url", url);

        return PendingIntent.GetActivity(
            context, 0, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
    }
}
