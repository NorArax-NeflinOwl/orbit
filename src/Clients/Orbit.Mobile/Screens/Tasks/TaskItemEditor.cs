using CommunityToolkit.Mvvm.ComponentModel;
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
    /// <inheritdoc cref="Inventory.WarehouseItemEditor.Channels"/>
    public static IReadOnlyList<string> Channels { get; } =
        [.. Enum.GetValues<NotificationChannel>().Select(channel => channel.ToString())];

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

    public static TaskItemEditor For(TaskItemDto item)
        => new(item)
        {
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
