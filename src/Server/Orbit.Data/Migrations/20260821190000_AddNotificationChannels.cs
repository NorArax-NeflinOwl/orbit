using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationChannels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- CalendarEvents: replace the two on/off flags with a per-channel choice ---
            migrationBuilder.AddColumn<string>(
                name: "CreationNotificationChannel",
                table: "CalendarEvents",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<string>(
                name: "ReminderNotificationChannel",
                table: "CalendarEvents",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "None");

            // Both flags predate push notifications (see AddPushNotifications), so "on" only ever meant
            // "e-mail" at the time it was set - backfilling to "Email" preserves exactly what an owner had
            // already opted into, rather than silently turning it off or switching it to push.
            migrationBuilder.Sql(
                """
                UPDATE CalendarEvents
                SET CreationNotificationChannel = CASE WHEN NotifyOnCreation = 1 THEN 'Email' ELSE 'None' END,
                    ReminderNotificationChannel = CASE WHEN NotifyBeforeStart = 1 THEN 'Email' ELSE 'None' END;
                """);

            migrationBuilder.DropColumn(
                name: "NotifyOnCreation",
                table: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "NotifyBeforeStart",
                table: "CalendarEvents");

            // --- TaskItemEntity: overdue notification channel, and the new "remind daily" reminder ---
            migrationBuilder.AddColumn<string>(
                name: "OverdueNotificationChannel",
                table: "TaskItemEntity",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "Push");

            migrationBuilder.AddColumn<bool>(
                name: "RemindDaily",
                table: "TaskItemEntity",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DailyReminderNotificationChannel",
                table: "TaskItemEntity",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "Push");

            migrationBuilder.AddColumn<int>(
                name: "DailyReminderTimeOfDayMinutes",
                table: "TaskItemEntity",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // --- TaskDailyReminderDeliveries: claim/delivery tracking for the daily reminder ---
            migrationBuilder.CreateTable(
                name: "TaskDailyReminderDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TaskItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReminderDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskDailyReminderDeliveries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskDailyReminderDeliveries_TaskItemId_ReminderDate",
                table: "TaskDailyReminderDeliveries",
                columns: new[] { "TaskItemId", "ReminderDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaskDailyReminderDeliveries");

            migrationBuilder.DropColumn(
                name: "DailyReminderTimeOfDayMinutes",
                table: "TaskItemEntity");

            migrationBuilder.DropColumn(
                name: "DailyReminderNotificationChannel",
                table: "TaskItemEntity");

            migrationBuilder.DropColumn(
                name: "RemindDaily",
                table: "TaskItemEntity");

            migrationBuilder.DropColumn(
                name: "OverdueNotificationChannel",
                table: "TaskItemEntity");

            migrationBuilder.AddColumn<bool>(
                name: "NotifyBeforeStart",
                table: "CalendarEvents",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyOnCreation",
                table: "CalendarEvents",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                """
                UPDATE CalendarEvents
                SET NotifyOnCreation = CASE WHEN CreationNotificationChannel IN ('Email', 'Push', 'Both') THEN 1 ELSE 0 END,
                    NotifyBeforeStart = CASE WHEN ReminderNotificationChannel IN ('Email', 'Push', 'Both') THEN 1 ELSE 0 END;
                """);

            migrationBuilder.DropColumn(
                name: "ReminderNotificationChannel",
                table: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "CreationNotificationChannel",
                table: "CalendarEvents");
        }
    }
}
