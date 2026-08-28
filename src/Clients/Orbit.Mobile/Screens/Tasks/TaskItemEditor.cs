using CommunityToolkit.Mvvm.ComponentModel;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens;
using Orbit.Contracts.Tasks;
using Orbit.Core.Notifications;

namespace Orbit.Mobile.Screens.Tasks;

/// <summary>
/// One task-list entry while it is being edited. Everything <see cref="TaskItemDto"/> carries and the
/// phone could not reach: when it is due, what happens when it goes overdue, and whether it says
/// something every day until it is done.
///
/// A separate object from the DTO for the same reason as WarehouseItemEditor: a form holds half-typed
/// values, and "no due date" and "a date being picked" are different states the DTO cannot express.
/// </summary>
public sealed partial class TaskItemEditor : ObservableObject
{
    /// <summary>
    /// What the web's dropdown offers, in the same order and with the same wording - see
    /// NotificationChannelChoice. Taken in the factory rather than set afterwards: the picker reads it
    /// once, when the form appears, and a list handed over after that is never looked at.
    /// </summary>
    public IReadOnlyList<NotificationChannelChoice> Channels { get; private init; } = [];

    /// <summary>Bound to the picker, which needs a choice out of Channels rather than a string.</summary>
    public NotificationChannelChoice? ChosenOverdueNotificationChannel
    {
        get => NotificationChannelChoice.For(Channels, OverdueNotificationChannel);
        set
        {
            if (value is not null)
            {
                OverdueNotificationChannel = value.Value;
            }
        }
    }

    /// <summary>Bound to the picker, which needs a choice out of Channels rather than a string.</summary>
    public NotificationChannelChoice? ChosenDailyReminderNotificationChannel
    {
        get => NotificationChannelChoice.For(Channels, DailyReminderNotificationChannel);
        set
        {
            if (value is not null)
            {
                DailyReminderNotificationChannel = value.Value;
            }
        }
    }

    private readonly TaskItemDto _item;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private bool _hasDueDate;

    [ObservableProperty]
    private DateTime _dueDate = DateTime.Today;

    [ObservableProperty]
    private string _overdueNotificationChannel = nameof(NotificationChannel.None);

    [ObservableProperty]
    private bool _remindDaily;

    [ObservableProperty]
    private string _dailyReminderNotificationChannel = nameof(NotificationChannel.None);

    [ObservableProperty]
    private TimeSpan _dailyReminderTime = new(9, 0, 0);

    private TaskItemEditor(TaskItemDto item) => _item = item;

    public static TaskItemEditor For(TaskItemDto item, Translations translations)
        => new(item)
        {
            Channels = NotificationChannelChoice.All(translations),
            Description = item.Description,
            HasDueDate = item.DueDateUtc is not null,
            DueDate = item.DueDateUtc?.LocalDateTime.Date ?? DateTime.Today,
            OverdueNotificationChannel = item.OverdueNotificationChannel,
            RemindDaily = item.RemindDaily,
            DailyReminderNotificationChannel = item.DailyReminderNotificationChannel,
            DailyReminderTime = item.DailyReminderTimeOfDay.ToTimeSpan()
        };

    public bool CanSave => Description.Trim().Length > 0;

    /// <summary>
    /// Everything this screen does not show - the id, whether it is done, what it is linked to - travels
    /// through untouched. An entry linked to an inventory item's restock task must come back linked.
    /// </summary>
    public TaskItemDto ToDto()
        => _item with
        {
            Description = Description.Trim(),
            DueDateUtc = HasDueDate
                ? new DateTimeOffset(DueDate.Date, TimeZoneInfo.Local.GetUtcOffset(DueDate.Date))
                : null,
            OverdueNotificationChannel = OverdueNotificationChannel,
            RemindDaily = RemindDaily,
            DailyReminderNotificationChannel = DailyReminderNotificationChannel,
            DailyReminderTimeOfDay = TimeOnly.FromTimeSpan(DailyReminderTime)
        };

    partial void OnDescriptionChanged(string value) => OnPropertyChanged(nameof(CanSave));
}
