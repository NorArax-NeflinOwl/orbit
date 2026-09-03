using Microsoft.EntityFrameworkCore;
using Orbit.Data.Entities;

namespace Orbit.Data;

/// <summary>
/// The physical name of every table and column, in one place.
///
/// Tables read as prefix_midfix[_postfix]. The prefix says what the table is for - OP_ holds what the
/// user works on, OL_ links rows in those tables to each other, OS_ carries system and account setup -
/// which is what makes an alphabetical listing group itself. Columns repeat their table's prefix,
/// shorten the midfix to its initials, and end with the property name, so a column carries its table
/// with it in a query that joins several: OP_N_ID belongs to OP_NOTES, OP_NS_ID to OP_NOTES_SHARED.
///
/// The map is exhaustive on purpose: a new entity that is not listed here fails at startup rather than
/// quietly taking EF's default name and drifting out of the convention.
/// </summary>
internal static class OrbitStorageNames
{
    private readonly record struct StorageName(string Table, string ColumnPrefix);

    private static readonly Dictionary<Type, StorageName> ByEntity = new()
    {
        // OP_ - what the user works on.
        [typeof(NoteEntity)] = new("OP_NOTES", "OP_N_"),
        [typeof(NoteShareEntity)] = new("OP_NOTES_SHARED", "OP_NS_"),
        [typeof(TaskEntity)] = new("OP_TASKS", "OP_T_"),
        [typeof(TaskItemEntity)] = new("OP_TASKS_ITEMS", "OP_TI_"),
        [typeof(TaskItemCategoryEntity)] = new("OP_TASKS_CATEGORIES", "OP_TC_"),
        [typeof(TaskShareEntity)] = new("OP_TASKS_SHARED", "OP_TS_"),
        [typeof(CalendarEventEntity)] = new("OP_EVENTS", "OP_E_"),
        [typeof(CalendarEventShareEntity)] = new("OP_EVENTS_SHARED", "OP_ES_"),
        [typeof(ChatMessageEntity)] = new("OP_CHATS", "OP_C_"),
        [typeof(ChatGroupEntity)] = new("OP_CHATS_GROUPS", "OP_CG_"),
        [typeof(ChatGroupAnnouncementEntity)] = new("OP_CHATS_ANNOUNCEMENTS", "OP_CA_"),
        [typeof(SharedLocationEntity)] = new("OP_LOCATIONS", "OP_L_"),
        [typeof(InventoryEntity)] = new("OP_INVENTORIES", "OP_I_"),
        [typeof(InventoryItemEntity)] = new("OP_INVENTORIES_ITEMS", "OP_II_"),
        [typeof(InventoryShareEntity)] = new("OP_INVENTORIES_SHARED", "OP_IS_"),
        [typeof(NotificationEntryEntity)] = new("OP_NOTIFICATIONS", "OP_NTF_"),

        // OL_ - rows that exist to join two of the tables above.
        [typeof(TaskItemTaskListLinkEntity)] = new("OL_TASKS_ITEMS", "OL_TI_"),
        [typeof(InventoryManagedTaskListEntity)] = new("OL_INVENTORIES_TASKS", "OL_IT_"),
        [typeof(ChatGroupMemberEntity)] = new("OL_CHATS_MEMBERS", "OL_CM_"),
        [typeof(ChatConversationAccessEntity)] = new("OL_CHATS_ACCESS", "OL_CA_"),
        [typeof(ContactEntity)] = new("OL_CONTACTS", "OL_C_"),
        [typeof(PublicShareLinkEntity)] = new("OL_PUBLIC_SHARES", "OL_PS_"),

        // OS_ - accounts, permissions, settings and the bookkeeping the system keeps for itself.
        [typeof(UserEntity)] = new("OS_USERS", "OS_U_"),
        [typeof(RefreshTokenEntity)] = new("OS_REFRESH_TOKENS", "OS_RT_"),
        [typeof(UserVerificationCodeEntity)] = new("OS_VERIFICATION_CODES", "OS_VC_"),
        [typeof(UserPermissionEntity)] = new("OS_USERS_PERMISSIONS", "OS_UP_"),
        [typeof(PermissionCodeEntity)] = new("OS_PERMISSIONS_CODES", "OS_PC_"),
        [typeof(NotificationSettingsEntity)] = new("OS_NOTIFICATIONS_SETTINGS", "OS_NTFS_"),
        [typeof(PushSubscriptionEntity)] = new("OS_PUSH_SUBSCRIPTIONS", "OS_PS_"),
        [typeof(DiagnosticLogEntryEntity)] = new("OS_DIAGNOSTICS", "OS_D_"),
        [typeof(SyncTombstoneEntity)] = new("OS_SYNC_TOMBSTONES", "OS_ST_"),
        [typeof(EventReminderDeliveryEntity)] = new("OS_EVENTS_REMINDERS", "OS_ER_"),
        [typeof(TaskDailyReminderDeliveryEntity)] = new("OS_TASKS_REMINDERS", "OS_TR_"),
        [typeof(TaskOverdueNotificationDeliveryEntity)] = new("OS_TASKS_OVERDUE", "OS_TO_"),
        [typeof(InventoryExpiryNotificationDeliveryEntity)] = new("OS_INVENTORIES_EXPIRY", "OS_IE_"),
    };

    /// <summary>
    /// Renames every table and column in the finished model. Runs last in OnModelCreating so it applies
    /// to shadow properties - foreign keys EF added on its own - as well as to declared ones.
    /// </summary>
    internal static void ApplyTo(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!ByEntity.TryGetValue(entityType.ClrType, out var name))
            {
                throw new InvalidOperationException(
                    $"{entityType.ClrType.Name} has no entry in {nameof(OrbitStorageNames)}. Add one - " +
                    "the naming convention is described there and in info/architecture.md.");
            }

            entityType.SetTableName(name.Table);
            foreach (var property in entityType.GetProperties())
            {
                property.SetColumnName(name.ColumnPrefix + property.Name.ToUpperInvariant());
            }
        }
    }
}
