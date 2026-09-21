using Orbit.Core.Notifications;
using Orbit.Core.Tasks.DailyReminders;
using Orbit.Core.Tasks.OverdueNotifications;
using Xunit;

namespace Orbit.Api.Tests.Notifications;

/// <summary>
/// Everything of one reader's that falls due in the same minute is one notice, not one each. Two
/// notices a second apart cost the reader the second of them: the web shows a banner for the newest
/// entry only and both clients keep a minimum gap between banners (see MainLayout and ForegroundNotices),
/// so the entry that arrived first was the one nobody saw. Reported by the user on 2026-09-21, with two
/// entries due at 17:00 and one banner between them.
///
/// What each channel says is built here rather than in the background services, so these tests pin the
/// wording without a poll loop - see OverdueTaskPushContent and its siblings.
/// </summary>
public sealed class OneNoticeForWhatFellDueTogetherTests
{
    private static readonly Guid Panda = Guid.NewGuid();
    private static readonly Guid Personal = Guid.NewGuid();
    private static readonly DateTimeOffset AtFive = new(2026, 9, 21, 17, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Two_overdue_entries_are_named_in_one_notice()
    {
        var payload = OverdueTaskPushContent.Build([AnOverdueEntry("Medicines", Panda), AnOverdueEntry("Chemist", Personal)]);

        Assert.Equal("Overdue tasks", payload.Title);
        Assert.Contains("Medicines", payload.Body);
        Assert.Contains("Chemist", payload.Body);
    }

    /// <summary>
    /// The lists are named beside the entries, because the same errand is often written on two of them
    /// and "Medicines, Medicines" would be a notice that answers nothing.
    /// </summary>
    [Fact]
    public void Each_entry_is_named_with_the_list_it_is_on()
    {
        var payload = OverdueTaskPushContent.Build([AnOverdueEntry("Medicines", Panda), AnOverdueEntry("Chemist", Personal)]);

        Assert.Contains("(Panda)", payload.Body);
        Assert.Contains("(Personal)", payload.Body);
    }

    /// <summary>One entry says what it always said - nothing has to read "these tasks" about one task.</summary>
    [Fact]
    public void One_overdue_entry_still_speaks_for_itself()
    {
        var payload = OverdueTaskPushContent.Build([AnOverdueEntry("Medicines", Panda)]);

        Assert.Equal("Overdue task", payload.Title);
        Assert.Equal($"/tasks/{Panda}", payload.Url);
    }

    [Fact]
    public void A_notice_about_one_list_leads_to_that_list()
    {
        var payload = OverdueTaskPushContent.Build([AnOverdueEntry("Medicines", Panda), AnOverdueEntry("Vitamins", Panda)]);

        Assert.Equal($"/tasks/{Panda}", payload.Url);
    }

    /// <summary>There is no page for "these two entries", so the lists themselves are where it lands.</summary>
    [Fact]
    public void A_notice_about_two_lists_leads_to_the_lists()
    {
        var payload = OverdueTaskPushContent.Build([AnOverdueEntry("Medicines", Panda), AnOverdueEntry("Chemist", Personal)]);

        Assert.Equal("/tasks", payload.Url);
    }

    [Fact]
    public void The_e_mail_says_how_many_and_names_each_of_them()
    {
        var (subject, body) = OverdueTaskEmailContent.Build(
            [AnOverdueEntry("Medicines", Panda), AnOverdueEntry("Chemist", Personal)]);

        Assert.Equal("2 overdue tasks", subject);
        Assert.Contains("Medicines", body);
        Assert.Contains("Chemist", body);
    }

    [Fact]
    public void Two_daily_reminders_are_one_reminder()
    {
        var payload = DailyTaskReminderPushContent.Build([AReminder("Medicines", Panda), AReminder("Chemist", Personal)]);

        Assert.Equal("Task reminders", payload.Title);
        Assert.Contains("Medicines", payload.Body);
        Assert.Contains("Chemist", payload.Body);
        Assert.Equal("/tasks", payload.Url);
    }

    [Fact]
    public void One_daily_reminder_still_speaks_for_itself()
    {
        var payload = DailyTaskReminderPushContent.Build([AReminder("Medicines", Panda)]);

        Assert.Equal("Task reminder", payload.Title);
        Assert.Equal($"/tasks/{Panda}", payload.Url);
    }

    [Fact]
    public void The_reminder_e_mail_names_each_of_them()
    {
        var (subject, body) = DailyTaskReminderEmailContent.Build(
            [AReminder("Medicines", Panda), AReminder("Chemist", Personal)]);

        Assert.Equal("2 reminders", subject);
        Assert.Contains("Medicines", body);
        Assert.Contains("Chemist", body);
    }

    private static OverdueTaskItem AnOverdueEntry(string description, Guid taskListId)
        => new(
            Guid.NewGuid(), taskListId, Guid.NewGuid(), TitleOf(taskListId), description, AtFive,
            NotificationChannel.Both);

    private static DueDailyTaskReminder AReminder(string description, Guid taskListId)
        => new(
            Guid.NewGuid(), taskListId, Guid.NewGuid(), TitleOf(taskListId), description, AtFive,
            NotificationChannel.Both, DateOnly.FromDateTime(AtFive.DateTime));

    private static string TitleOf(Guid taskListId) => taskListId == Panda ? "Panda" : "Personal";
}
