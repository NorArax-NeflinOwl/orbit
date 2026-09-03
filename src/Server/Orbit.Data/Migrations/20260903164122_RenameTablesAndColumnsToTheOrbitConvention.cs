using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameTablesAndColumnsToTheOrbitConvention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // EF scaffolds a drop-and-create for these two, because renaming their entity classes
            // (WarehouseEntity -> InventoryEntity) leaves its differ with nothing to match the old
            // tables against. That would take every inventory and every share of one with it, so the
            // rename is written out by hand here and the rows survive the deploy.
            migrationBuilder.RenameTable(
                name: "Warehouses",
                newName: "OP_INVENTORIES");

            migrationBuilder.RenameTable(
                name: "WarehouseShares",
                newName: "OP_INVENTORIES_SHARED");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "OP_INVENTORIES",
                newName: "OP_I_ID");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "OP_INVENTORIES",
                newName: "OP_I_CREATEDATUTC");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "OP_INVENTORIES",
                newName: "OP_I_DESCRIPTION");

            migrationBuilder.RenameColumn(
                name: "EncryptedCiphertext",
                table: "OP_INVENTORIES",
                newName: "OP_I_ENCRYPTEDCIPHERTEXT");

            migrationBuilder.RenameColumn(
                name: "EncryptedNonce",
                table: "OP_INVENTORIES",
                newName: "OP_I_ENCRYPTEDNONCE");

            migrationBuilder.RenameColumn(
                name: "IsPrivate",
                table: "OP_INVENTORIES",
                newName: "OP_I_ISPRIVATE");

            migrationBuilder.RenameColumn(
                name: "LockExpiresAtUtc",
                table: "OP_INVENTORIES",
                newName: "OP_I_LOCKEXPIRESATUTC");

            migrationBuilder.RenameColumn(
                name: "LockedByUserId",
                table: "OP_INVENTORIES",
                newName: "OP_I_LOCKEDBYUSERID");

            migrationBuilder.RenameColumn(
                name: "LockedByUserName",
                table: "OP_INVENTORIES",
                newName: "OP_I_LOCKEDBYUSERNAME");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "OP_INVENTORIES",
                newName: "OP_I_NAME");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "OP_INVENTORIES",
                newName: "OP_I_UPDATEDATUTC");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "OP_INVENTORIES",
                newName: "OP_I_USERID");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "OP_INVENTORIES_SHARED",
                newName: "OP_IS_ID");

            migrationBuilder.RenameColumn(
                name: "AcceptedAtUtc",
                table: "OP_INVENTORIES_SHARED",
                newName: "OP_IS_ACCEPTEDATUTC");

            migrationBuilder.RenameColumn(
                name: "AccessLevel",
                table: "OP_INVENTORIES_SHARED",
                newName: "OP_IS_ACCESSLEVEL");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "OP_INVENTORIES_SHARED",
                newName: "OP_IS_CREATEDATUTC");

            migrationBuilder.RenameColumn(
                name: "OwnerUserId",
                table: "OP_INVENTORIES_SHARED",
                newName: "OP_IS_OWNERUSERID");

            migrationBuilder.RenameColumn(
                name: "RecipientUserId",
                table: "OP_INVENTORIES_SHARED",
                newName: "OP_IS_RECIPIENTUSERID");

            migrationBuilder.RenameColumn(
                name: "SourceWarehouseId",
                table: "OP_INVENTORIES_SHARED",
                newName: "OP_IS_SOURCEINVENTORYID");

            migrationBuilder.RenameIndex(
                name: "ix_warehouses_name_trgm",
                table: "OP_INVENTORIES",
                newName: "ix_inventories_name_trgm");

            migrationBuilder.RenameIndex(
                name: "IX_Warehouses_UserId",
                table: "OP_INVENTORIES",
                newName: "IX_OP_INVENTORIES_OP_I_USERID");

            migrationBuilder.RenameIndex(
                name: "IX_WarehouseShares_RecipientUserId",
                table: "OP_INVENTORIES_SHARED",
                newName: "IX_OP_INVENTORIES_SHARED_OP_IS_RECIPIENTUSERID");

            migrationBuilder.RenameIndex(
                name: "IX_WarehouseShares_SourceWarehouseId_RecipientUserId",
                table: "OP_INVENTORIES_SHARED",
                newName: "IX_OP_INVENTORIES_SHARED_OP_IS_SOURCEINVENTORYID_OP_IS_RECIPIE~");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Warehouses",
                table: "OP_INVENTORIES");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OP_INVENTORIES",
                table: "OP_INVENTORIES",
                column: "OP_I_ID");

            migrationBuilder.DropPrimaryKey(
                name: "PK_WarehouseShares",
                table: "OP_INVENTORIES_SHARED");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OP_INVENTORIES_SHARED",
                table: "OP_INVENTORIES_SHARED",
                column: "OP_IS_ID");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatGroupAnnouncements_ChatGroups_GroupId",
                table: "ChatGroupAnnouncements");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatGroupMembers_ChatGroups_GroupId",
                table: "ChatGroupMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskItemCategoryEntity_TaskItemEntity_TaskItemId",
                table: "TaskItemCategoryEntity");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskItemEntity_Tasks_TaskId",
                table: "TaskItemEntity");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskItemTaskListLinkEntity_TaskItemEntity_TaskItemId",
                table: "TaskItemTaskListLinkEntity");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserVerificationCodes",
                table: "UserVerificationCodes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Users",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserPermissions",
                table: "UserPermissions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TaskShares",
                table: "TaskShares");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Tasks",
                table: "Tasks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TaskOverdueNotificationDeliveries",
                table: "TaskOverdueNotificationDeliveries");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TaskItemTaskListLinkEntity",
                table: "TaskItemTaskListLinkEntity");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TaskItemEntity",
                table: "TaskItemEntity");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TaskItemCategoryEntity",
                table: "TaskItemCategoryEntity");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TaskDailyReminderDeliveries",
                table: "TaskDailyReminderDeliveries");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SyncTombstones",
                table: "SyncTombstones");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SharedLocations",
                table: "SharedLocations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RefreshTokens",
                table: "RefreshTokens");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PushSubscriptions",
                table: "PushSubscriptions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PublicShareLinks",
                table: "PublicShareLinks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PermissionCodes",
                table: "PermissionCodes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_NotificationSettings",
                table: "NotificationSettings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_NotificationEntries",
                table: "NotificationEntries");

            migrationBuilder.DropPrimaryKey(
                name: "PK_NoteShares",
                table: "NoteShares");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Notes",
                table: "Notes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_InventoryManagedTaskLists",
                table: "InventoryManagedTaskLists");

            migrationBuilder.DropPrimaryKey(
                name: "PK_InventoryItems",
                table: "InventoryItems");

            migrationBuilder.DropPrimaryKey(
                name: "PK_InventoryExpiryNotificationDeliveries",
                table: "InventoryExpiryNotificationDeliveries");

            migrationBuilder.DropPrimaryKey(
                name: "PK_EventReminderDeliveries",
                table: "EventReminderDeliveries");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DiagnosticLogEntries",
                table: "DiagnosticLogEntries");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Contacts",
                table: "Contacts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ChatMessages",
                table: "ChatMessages");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ChatGroups",
                table: "ChatGroups");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ChatGroupMembers",
                table: "ChatGroupMembers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ChatGroupAnnouncements",
                table: "ChatGroupAnnouncements");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ChatConversationAccesses",
                table: "ChatConversationAccesses");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CalendarEventShares",
                table: "CalendarEventShares");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CalendarEvents",
                table: "CalendarEvents");

            migrationBuilder.RenameTable(
                name: "UserVerificationCodes",
                newName: "OS_VERIFICATION_CODES");

            migrationBuilder.RenameTable(
                name: "Users",
                newName: "OS_USERS");

            migrationBuilder.RenameTable(
                name: "UserPermissions",
                newName: "OS_USERS_PERMISSIONS");

            migrationBuilder.RenameTable(
                name: "TaskShares",
                newName: "OP_TASKS_SHARED");

            migrationBuilder.RenameTable(
                name: "Tasks",
                newName: "OP_TASKS");

            migrationBuilder.RenameTable(
                name: "TaskOverdueNotificationDeliveries",
                newName: "OS_TASKS_OVERDUE");

            migrationBuilder.RenameTable(
                name: "TaskItemTaskListLinkEntity",
                newName: "OL_TASKS_ITEMS");

            migrationBuilder.RenameTable(
                name: "TaskItemEntity",
                newName: "OP_TASKS_ITEMS");

            migrationBuilder.RenameTable(
                name: "TaskItemCategoryEntity",
                newName: "OP_TASKS_CATEGORIES");

            migrationBuilder.RenameTable(
                name: "TaskDailyReminderDeliveries",
                newName: "OS_TASKS_REMINDERS");

            migrationBuilder.RenameTable(
                name: "SyncTombstones",
                newName: "OS_SYNC_TOMBSTONES");

            migrationBuilder.RenameTable(
                name: "SharedLocations",
                newName: "OP_LOCATIONS");

            migrationBuilder.RenameTable(
                name: "RefreshTokens",
                newName: "OS_REFRESH_TOKENS");

            migrationBuilder.RenameTable(
                name: "PushSubscriptions",
                newName: "OS_PUSH_SUBSCRIPTIONS");

            migrationBuilder.RenameTable(
                name: "PublicShareLinks",
                newName: "OL_PUBLIC_SHARES");

            migrationBuilder.RenameTable(
                name: "PermissionCodes",
                newName: "OS_PERMISSIONS_CODES");

            migrationBuilder.RenameTable(
                name: "NotificationSettings",
                newName: "OS_NOTIFICATIONS_SETTINGS");

            migrationBuilder.RenameTable(
                name: "NotificationEntries",
                newName: "OP_NOTIFICATIONS");

            migrationBuilder.RenameTable(
                name: "NoteShares",
                newName: "OP_NOTES_SHARED");

            migrationBuilder.RenameTable(
                name: "Notes",
                newName: "OP_NOTES");

            migrationBuilder.RenameTable(
                name: "InventoryManagedTaskLists",
                newName: "OL_INVENTORIES_TASKS");

            migrationBuilder.RenameTable(
                name: "InventoryItems",
                newName: "OP_INVENTORIES_ITEMS");

            migrationBuilder.RenameTable(
                name: "InventoryExpiryNotificationDeliveries",
                newName: "OS_INVENTORIES_EXPIRY");

            migrationBuilder.RenameTable(
                name: "EventReminderDeliveries",
                newName: "OS_EVENTS_REMINDERS");

            migrationBuilder.RenameTable(
                name: "DiagnosticLogEntries",
                newName: "OS_DIAGNOSTICS");

            migrationBuilder.RenameTable(
                name: "Contacts",
                newName: "OL_CONTACTS");

            migrationBuilder.RenameTable(
                name: "ChatMessages",
                newName: "OP_CHATS");

            migrationBuilder.RenameTable(
                name: "ChatGroups",
                newName: "OP_CHATS_GROUPS");

            migrationBuilder.RenameTable(
                name: "ChatGroupMembers",
                newName: "OL_CHATS_MEMBERS");

            migrationBuilder.RenameTable(
                name: "ChatGroupAnnouncements",
                newName: "OP_CHATS_ANNOUNCEMENTS");

            migrationBuilder.RenameTable(
                name: "ChatConversationAccesses",
                newName: "OL_CHATS_ACCESS");

            migrationBuilder.RenameTable(
                name: "CalendarEventShares",
                newName: "OP_EVENTS_SHARED");

            migrationBuilder.RenameTable(
                name: "CalendarEvents",
                newName: "OP_EVENTS");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "OS_VERIFICATION_CODES",
                newName: "OS_VC_USERID");

            migrationBuilder.RenameColumn(
                name: "Purpose",
                table: "OS_VERIFICATION_CODES",
                newName: "OS_VC_PURPOSE");

            migrationBuilder.RenameColumn(
                name: "FailedAttempts",
                table: "OS_VERIFICATION_CODES",
                newName: "OS_VC_FAILEDATTEMPTS");

            migrationBuilder.RenameColumn(
                name: "ExpiresAtUtc",
                table: "OS_VERIFICATION_CODES",
                newName: "OS_VC_EXPIRESATUTC");

            migrationBuilder.RenameColumn(
                name: "EmailAddress",
                table: "OS_VERIFICATION_CODES",
                newName: "OS_VC_EMAILADDRESS");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "OS_VERIFICATION_CODES",
                newName: "OS_VC_CREATEDATUTC");

            migrationBuilder.RenameColumn(
                name: "ConsumedAtUtc",
                table: "OS_VERIFICATION_CODES",
                newName: "OS_VC_CONSUMEDATUTC");

            migrationBuilder.RenameColumn(
                name: "CodeHash",
                table: "OS_VERIFICATION_CODES",
                newName: "OS_VC_CODEHASH");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "OS_VERIFICATION_CODES",
                newName: "OS_VC_ID");

            migrationBuilder.RenameIndex(
                name: "IX_UserVerificationCodes_UserId_Purpose",
                table: "OS_VERIFICATION_CODES",
                newName: "IX_OS_VERIFICATION_CODES_OS_VC_USERID_OS_VC_PURPOSE");

            migrationBuilder.RenameColumn(
                name: "WrappedPrivateKeyBase64",
                table: "OS_USERS",
                newName: "OS_U_WRAPPEDPRIVATEKEYBASE64");

            migrationBuilder.RenameColumn(
                name: "UserName",
                table: "OS_USERS",
                newName: "OS_U_USERNAME");

            migrationBuilder.RenameColumn(
                name: "PublicKeyBase64",
                table: "OS_USERS",
                newName: "OS_U_PUBLICKEYBASE64");

            migrationBuilder.RenameColumn(
                name: "PrivateKeyWrapNonceBase64",
                table: "OS_USERS",
                newName: "OS_U_PRIVATEKEYWRAPNONCEBASE64");

            migrationBuilder.RenameColumn(
                name: "PrivateKeySaltBase64",
                table: "OS_USERS",
                newName: "OS_U_PRIVATEKEYSALTBASE64");

            migrationBuilder.RenameColumn(
                name: "PrivateKeyDerivationIterations",
                table: "OS_USERS",
                newName: "OS_U_PRIVATEKEYDERIVATIONITERATIONS");

            migrationBuilder.RenameColumn(
                name: "PresenceLastSeenAtUtc",
                table: "OS_USERS",
                newName: "OS_U_PRESENCELASTSEENATUTC");

            migrationBuilder.RenameColumn(
                name: "PresenceAvailability",
                table: "OS_USERS",
                newName: "OS_U_PRESENCEAVAILABILITY");

            migrationBuilder.RenameColumn(
                name: "PasswordHash",
                table: "OS_USERS",
                newName: "OS_U_PASSWORDHASH");

            migrationBuilder.RenameColumn(
                name: "LocationRecordedAtUtc",
                table: "OS_USERS",
                newName: "OS_U_LOCATIONRECORDEDATUTC");

            migrationBuilder.RenameColumn(
                name: "LocationLongitude",
                table: "OS_USERS",
                newName: "OS_U_LOCATIONLONGITUDE");

            migrationBuilder.RenameColumn(
                name: "LocationLatitude",
                table: "OS_USERS",
                newName: "OS_U_LOCATIONLATITUDE");

            migrationBuilder.RenameColumn(
                name: "LocationAddress",
                table: "OS_USERS",
                newName: "OS_U_LOCATIONADDRESS");

            migrationBuilder.RenameColumn(
                name: "GoogleSubjectId",
                table: "OS_USERS",
                newName: "OS_U_GOOGLESUBJECTID");

            migrationBuilder.RenameColumn(
                name: "EmailVerifiedAtUtc",
                table: "OS_USERS",
                newName: "OS_U_EMAILVERIFIEDATUTC");

            migrationBuilder.RenameColumn(
                name: "Email",
                table: "OS_USERS",
                newName: "OS_U_EMAIL");

            migrationBuilder.RenameColumn(
                name: "DisplayName",
                table: "OS_USERS",
                newName: "OS_U_DISPLAYNAME");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "OS_USERS",
                newName: "OS_U_CREATEDATUTC");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "OS_USERS",
                newName: "OS_U_ID");

            migrationBuilder.RenameIndex(
                name: "IX_Users_UserName",
                table: "OS_USERS",
                newName: "IX_OS_USERS_OS_U_USERNAME");

            migrationBuilder.RenameIndex(
                name: "IX_Users_GoogleSubjectId",
                table: "OS_USERS",
                newName: "IX_OS_USERS_OS_U_GOOGLESUBJECTID");

            migrationBuilder.RenameIndex(
                name: "IX_Users_Email",
                table: "OS_USERS",
                newName: "IX_OS_USERS_OS_U_EMAIL");

            migrationBuilder.RenameColumn(
                name: "GrantedAtUtc",
                table: "OS_USERS_PERMISSIONS",
                newName: "OS_UP_GRANTEDATUTC");

            migrationBuilder.RenameColumn(
                name: "Permission",
                table: "OS_USERS_PERMISSIONS",
                newName: "OS_UP_PERMISSION");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "OS_USERS_PERMISSIONS",
                newName: "OS_UP_USERID");

            migrationBuilder.RenameColumn(
                name: "SourceTaskListId",
                table: "OP_TASKS_SHARED",
                newName: "OP_TS_SOURCETASKLISTID");

            migrationBuilder.RenameColumn(
                name: "RecipientUserId",
                table: "OP_TASKS_SHARED",
                newName: "OP_TS_RECIPIENTUSERID");

            migrationBuilder.RenameColumn(
                name: "OwnerUserId",
                table: "OP_TASKS_SHARED",
                newName: "OP_TS_OWNERUSERID");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "OP_TASKS_SHARED",
                newName: "OP_TS_CREATEDATUTC");

            migrationBuilder.RenameColumn(
                name: "AccessLevel",
                table: "OP_TASKS_SHARED",
                newName: "OP_TS_ACCESSLEVEL");

            migrationBuilder.RenameColumn(
                name: "AcceptedAtUtc",
                table: "OP_TASKS_SHARED",
                newName: "OP_TS_ACCEPTEDATUTC");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "OP_TASKS_SHARED",
                newName: "OP_TS_ID");

            migrationBuilder.RenameIndex(
                name: "IX_TaskShares_SourceTaskListId_RecipientUserId",
                table: "OP_TASKS_SHARED",
                newName: "IX_OP_TASKS_SHARED_OP_TS_SOURCETASKLISTID_OP_TS_RECIPIENTUSERID");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "OP_TASKS",
                newName: "OP_T_USERID");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "OP_TASKS",
                newName: "OP_T_UPDATEDATUTC");

            migrationBuilder.RenameColumn(
                name: "Title",
                table: "OP_TASKS",
                newName: "OP_T_TITLE");

            migrationBuilder.RenameColumn(
                name: "Priority",
                table: "OP_TASKS",
                newName: "OP_T_PRIORITY");

            migrationBuilder.RenameColumn(
                name: "LockedByUserName",
                table: "OP_TASKS",
                newName: "OP_T_LOCKEDBYUSERNAME");

            migrationBuilder.RenameColumn(
                name: "LockedByUserId",
                table: "OP_TASKS",
                newName: "OP_T_LOCKEDBYUSERID");

            migrationBuilder.RenameColumn(
                name: "LockExpiresAtUtc",
                table: "OP_TASKS",
                newName: "OP_T_LOCKEXPIRESATUTC");

            migrationBuilder.RenameColumn(
                name: "IsPrivate",
                table: "OP_TASKS",
                newName: "OP_T_ISPRIVATE");

            migrationBuilder.RenameColumn(
                name: "IsPinned",
                table: "OP_TASKS",
                newName: "OP_T_ISPINNED");

            migrationBuilder.RenameColumn(
                name: "IsGroup",
                table: "OP_TASKS",
                newName: "OP_T_ISGROUP");

            migrationBuilder.RenameColumn(
                name: "IsCompleted",
                table: "OP_TASKS",
                newName: "OP_T_ISCOMPLETED");

            migrationBuilder.RenameColumn(
                name: "EncryptedNonce",
                table: "OP_TASKS",
                newName: "OP_T_ENCRYPTEDNONCE");

            migrationBuilder.RenameColumn(
                name: "EncryptedCiphertext",
                table: "OP_TASKS",
                newName: "OP_T_ENCRYPTEDCIPHERTEXT");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "OP_TASKS",
                newName: "OP_T_DESCRIPTION");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "OP_TASKS",
                newName: "OP_T_CREATEDATUTC");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "OP_TASKS",
                newName: "OP_T_ID");

            migrationBuilder.RenameColumn(
                name: "LinkedWarehouseId",
                table: "OP_TASKS",
                newName: "OP_T_LINKEDINVENTORYID");

            migrationBuilder.RenameIndex(
                name: "IX_Tasks_UserId",
                table: "OP_TASKS",
                newName: "IX_OP_TASKS_OP_T_USERID");

            migrationBuilder.RenameColumn(
                name: "TaskItemId",
                table: "OS_TASKS_OVERDUE",
                newName: "OS_TO_TASKITEMID");

            migrationBuilder.RenameColumn(
                name: "SentAtUtc",
                table: "OS_TASKS_OVERDUE",
                newName: "OS_TO_SENTATUTC");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "OS_TASKS_OVERDUE",
                newName: "OS_TO_ID");

            migrationBuilder.RenameIndex(
                name: "IX_TaskOverdueNotificationDeliveries_TaskItemId",
                table: "OS_TASKS_OVERDUE",
                newName: "IX_OS_TASKS_OVERDUE_OS_TO_TASKITEMID");

            migrationBuilder.RenameColumn(
                name: "Position",
                table: "OL_TASKS_ITEMS",
                newName: "OL_TI_POSITION");

            migrationBuilder.RenameColumn(
                name: "LinkedTaskListId",
                table: "OL_TASKS_ITEMS",
                newName: "OL_TI_LINKEDTASKLISTID");

            migrationBuilder.RenameColumn(
                name: "TaskItemId",
                table: "OL_TASKS_ITEMS",
                newName: "OL_TI_TASKITEMID");

            migrationBuilder.RenameIndex(
                name: "IX_TaskItemTaskListLinkEntity_LinkedTaskListId",
                table: "OL_TASKS_ITEMS",
                newName: "IX_OL_TASKS_ITEMS_OL_TI_LINKEDTASKLISTID");

            migrationBuilder.RenameColumn(
                name: "TaskId",
                table: "OP_TASKS_ITEMS",
                newName: "OP_TI_TASKID");

            migrationBuilder.RenameColumn(
                name: "RemindDaily",
                table: "OP_TASKS_ITEMS",
                newName: "OP_TI_REMINDDAILY");

            migrationBuilder.RenameColumn(
                name: "Position",
                table: "OP_TASKS_ITEMS",
                newName: "OP_TI_POSITION");

            migrationBuilder.RenameColumn(
                name: "OverdueNotificationChannel",
                table: "OP_TASKS_ITEMS",
                newName: "OP_TI_OVERDUENOTIFICATIONCHANNEL");

            migrationBuilder.RenameColumn(
                name: "Location",
                table: "OP_TASKS_ITEMS",
                newName: "OP_TI_LOCATION");

            migrationBuilder.RenameColumn(
                name: "LinkedInventoryItemId",
                table: "OP_TASKS_ITEMS",
                newName: "OP_TI_LINKEDINVENTORYITEMID");

            migrationBuilder.RenameColumn(
                name: "LinkedCalendarEventId",
                table: "OP_TASKS_ITEMS",
                newName: "OP_TI_LINKEDCALENDAREVENTID");

            migrationBuilder.RenameColumn(
                name: "Kind",
                table: "OP_TASKS_ITEMS",
                newName: "OP_TI_KIND");

            migrationBuilder.RenameColumn(
                name: "IsCompleted",
                table: "OP_TASKS_ITEMS",
                newName: "OP_TI_ISCOMPLETED");

            migrationBuilder.RenameColumn(
                name: "DueDateUtc",
                table: "OP_TASKS_ITEMS",
                newName: "OP_TI_DUEDATEUTC");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "OP_TASKS_ITEMS",
                newName: "OP_TI_DESCRIPTION");

            migrationBuilder.RenameColumn(
                name: "DailyReminderTimeOfDayMinutes",
                table: "OP_TASKS_ITEMS",
                newName: "OP_TI_DAILYREMINDERTIMEOFDAYMINUTES");

            migrationBuilder.RenameColumn(
                name: "DailyReminderNotificationChannel",
                table: "OP_TASKS_ITEMS",
                newName: "OP_TI_DAILYREMINDERNOTIFICATIONCHANNEL");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "OP_TASKS_ITEMS",
                newName: "OP_TI_ID");

            migrationBuilder.RenameIndex(
                name: "IX_TaskItemEntity_TaskId",
                table: "OP_TASKS_ITEMS",
                newName: "IX_OP_TASKS_ITEMS_OP_TI_TASKID");

            migrationBuilder.RenameColumn(
                name: "Position",
                table: "OP_TASKS_CATEGORIES",
                newName: "OP_TC_POSITION");

            migrationBuilder.RenameColumn(
                name: "Category",
                table: "OP_TASKS_CATEGORIES",
                newName: "OP_TC_CATEGORY");

            migrationBuilder.RenameColumn(
                name: "TaskItemId",
                table: "OP_TASKS_CATEGORIES",
                newName: "OP_TC_TASKITEMID");

            migrationBuilder.RenameIndex(
                name: "IX_TaskItemCategoryEntity_Category",
                table: "OP_TASKS_CATEGORIES",
                newName: "IX_OP_TASKS_CATEGORIES_OP_TC_CATEGORY");

            migrationBuilder.RenameColumn(
                name: "TaskItemId",
                table: "OS_TASKS_REMINDERS",
                newName: "OS_TR_TASKITEMID");

            migrationBuilder.RenameColumn(
                name: "SentAtUtc",
                table: "OS_TASKS_REMINDERS",
                newName: "OS_TR_SENTATUTC");

            migrationBuilder.RenameColumn(
                name: "ReminderDate",
                table: "OS_TASKS_REMINDERS",
                newName: "OS_TR_REMINDERDATE");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "OS_TASKS_REMINDERS",
                newName: "OS_TR_ID");

            migrationBuilder.RenameIndex(
                name: "IX_TaskDailyReminderDeliveries_TaskItemId_ReminderDate",
                table: "OS_TASKS_REMINDERS",
                newName: "IX_OS_TASKS_REMINDERS_OS_TR_TASKITEMID_OS_TR_REMINDERDATE");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "OS_SYNC_TOMBSTONES",
                newName: "OS_ST_USERID");

            migrationBuilder.RenameColumn(
                name: "EntityType",
                table: "OS_SYNC_TOMBSTONES",
                newName: "OS_ST_ENTITYTYPE");

            migrationBuilder.RenameColumn(
                name: "EntityId",
                table: "OS_SYNC_TOMBSTONES",
                newName: "OS_ST_ENTITYID");

            migrationBuilder.RenameColumn(
                name: "DeletedAtUtc",
                table: "OS_SYNC_TOMBSTONES",
                newName: "OS_ST_DELETEDATUTC");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "OS_SYNC_TOMBSTONES",
                newName: "OS_ST_ID");

            migrationBuilder.RenameIndex(
                name: "IX_SyncTombstones_UserId_EntityType_DeletedAtUtc",
                table: "OS_SYNC_TOMBSTONES",
                newName: "IX_OS_SYNC_TOMBSTONES_OS_ST_USERID_OS_ST_ENTITYTYPE_OS_ST_DELE~");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "OP_LOCATIONS",
                newName: "OP_L_UPDATEDATUTC");

            migrationBuilder.RenameColumn(
                name: "SharerUserId",
                table: "OP_LOCATIONS",
                newName: "OP_L_SHARERUSERID");

            migrationBuilder.RenameColumn(
                name: "RecipientUserId",
                table: "OP_LOCATIONS",
                newName: "OP_L_RECIPIENTUSERID");

            migrationBuilder.RenameColumn(
                name: "NonceBase64",
                table: "OP_LOCATIONS",
                newName: "OP_L_NONCEBASE64");

            migrationBuilder.RenameColumn(
                name: "IsContinuous",
                table: "OP_LOCATIONS",
                newName: "OP_L_ISCONTINUOUS");

            migrationBuilder.RenameColumn(
                name: "CiphertextBase64",
                table: "OP_LOCATIONS",
                newName: "OP_L_CIPHERTEXTBASE64");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "OP_LOCATIONS",
                newName: "OP_L_ID");

            migrationBuilder.RenameIndex(
                name: "IX_SharedLocations_SharerUserId_RecipientUserId",
                table: "OP_LOCATIONS",
                newName: "IX_OP_LOCATIONS_OP_L_SHARERUSERID_OP_L_RECIPIENTUSERID");

            migrationBuilder.RenameIndex(
                name: "IX_SharedLocations_RecipientUserId",
                table: "OP_LOCATIONS",
                newName: "IX_OP_LOCATIONS_OP_L_RECIPIENTUSERID");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "OS_REFRESH_TOKENS",
                newName: "OS_RT_USERID");

            migrationBuilder.RenameColumn(
                name: "TokenHash",
                table: "OS_REFRESH_TOKENS",
                newName: "OS_RT_TOKENHASH");

            migrationBuilder.RenameColumn(
                name: "RevokedAtUtc",
                table: "OS_REFRESH_TOKENS",
                newName: "OS_RT_REVOKEDATUTC");

            migrationBuilder.RenameColumn(
                name: "ExpiresAtUtc",
                table: "OS_REFRESH_TOKENS",
                newName: "OS_RT_EXPIRESATUTC");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "OS_REFRESH_TOKENS",
                newName: "OS_RT_CREATEDATUTC");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "OS_REFRESH_TOKENS",
                newName: "OS_RT_ID");

            migrationBuilder.RenameIndex(
                name: "IX_RefreshTokens_UserId",
                table: "OS_REFRESH_TOKENS",
                newName: "IX_OS_REFRESH_TOKENS_OS_RT_USERID");

            migrationBuilder.RenameIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "OS_REFRESH_TOKENS",
                newName: "IX_OS_REFRESH_TOKENS_OS_RT_TOKENHASH");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "OS_PUSH_SUBSCRIPTIONS",
                newName: "OS_PS_USERID");

            migrationBuilder.RenameColumn(
                name: "Transport",
                table: "OS_PUSH_SUBSCRIPTIONS",
                newName: "OS_PS_TRANSPORT");

            migrationBuilder.RenameColumn(
                name: "P256dhBase64",
                table: "OS_PUSH_SUBSCRIPTIONS",
                newName: "OS_PS_P256DHBASE64");

            migrationBuilder.RenameColumn(
                name: "Endpoint",
                table: "OS_PUSH_SUBSCRIPTIONS",
                newName: "OS_PS_ENDPOINT");

            migrationBuilder.RenameColumn(
                name: "DeviceToken",
                table: "OS_PUSH_SUBSCRIPTIONS",
                newName: "OS_PS_DEVICETOKEN");

            migrationBuilder.RenameColumn(
                name: "DevicePlatform",
                table: "OS_PUSH_SUBSCRIPTIONS",
                newName: "OS_PS_DEVICEPLATFORM");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "OS_PUSH_SUBSCRIPTIONS",
                newName: "OS_PS_CREATEDATUTC");

            migrationBuilder.RenameColumn(
                name: "AuthBase64",
                table: "OS_PUSH_SUBSCRIPTIONS",
                newName: "OS_PS_AUTHBASE64");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "OS_PUSH_SUBSCRIPTIONS",
                newName: "OS_PS_ID");

            migrationBuilder.RenameIndex(
                name: "IX_PushSubscriptions_UserId",
                table: "OS_PUSH_SUBSCRIPTIONS",
                newName: "IX_OS_PUSH_SUBSCRIPTIONS_OS_PS_USERID");

            migrationBuilder.RenameIndex(
                name: "IX_PushSubscriptions_Endpoint",
                table: "OS_PUSH_SUBSCRIPTIONS",
                newName: "IX_OS_PUSH_SUBSCRIPTIONS_OS_PS_ENDPOINT");

            migrationBuilder.RenameIndex(
                name: "IX_PushSubscriptions_DeviceToken",
                table: "OS_PUSH_SUBSCRIPTIONS",
                newName: "IX_OS_PUSH_SUBSCRIPTIONS_OS_PS_DEVICETOKEN");

            migrationBuilder.RenameColumn(
                name: "Token",
                table: "OL_PUBLIC_SHARES",
                newName: "OL_PS_TOKEN");

            migrationBuilder.RenameColumn(
                name: "RevokedAtUtc",
                table: "OL_PUBLIC_SHARES",
                newName: "OL_PS_REVOKEDATUTC");

            migrationBuilder.RenameColumn(
                name: "OwnerUserId",
                table: "OL_PUBLIC_SHARES",
                newName: "OL_PS_OWNERUSERID");

            migrationBuilder.RenameColumn(
                name: "ItemType",
                table: "OL_PUBLIC_SHARES",
                newName: "OL_PS_ITEMTYPE");

            migrationBuilder.RenameColumn(
                name: "ItemId",
                table: "OL_PUBLIC_SHARES",
                newName: "OL_PS_ITEMID");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "OL_PUBLIC_SHARES",
                newName: "OL_PS_CREATEDATUTC");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "OL_PUBLIC_SHARES",
                newName: "OL_PS_ID");

            migrationBuilder.RenameIndex(
                name: "IX_PublicShareLinks_Token",
                table: "OL_PUBLIC_SHARES",
                newName: "IX_OL_PUBLIC_SHARES_OL_PS_TOKEN");

            migrationBuilder.RenameIndex(
                name: "IX_PublicShareLinks_OwnerUserId_ItemType_ItemId",
                table: "OL_PUBLIC_SHARES",
                newName: "IX_OL_PUBLIC_SHARES_OL_PS_OWNERUSERID_OL_PS_ITEMTYPE_OL_PS_ITE~");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "OS_PERMISSIONS_CODES",
                newName: "OS_PC_CREATEDATUTC");

            migrationBuilder.RenameColumn(
                name: "Code",
                table: "OS_PERMISSIONS_CODES",
                newName: "OS_PC_CODE");

            migrationBuilder.RenameColumn(
                name: "Permission",
                table: "OS_PERMISSIONS_CODES",
                newName: "OS_PC_PERMISSION");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "OS_NOTIFICATIONS_SETTINGS",
                newName: "OS_NTFS_USERID");

            migrationBuilder.RenameColumn(
                name: "ShowExceptionDetails",
                table: "OS_NOTIFICATIONS_SETTINGS",
                newName: "OS_NTFS_SHOWEXCEPTIONDETAILS");

            migrationBuilder.RenameColumn(
                name: "RetentionDays",
                table: "OS_NOTIFICATIONS_SETTINGS",
                newName: "OS_NTFS_RETENTIONDAYS");

            migrationBuilder.RenameColumn(
                name: "BannerVisibleSeconds",
                table: "OS_NOTIFICATIONS_SETTINGS",
                newName: "OS_NTFS_BANNERVISIBLESECONDS");

            migrationBuilder.RenameColumn(
                name: "BannerMinimumGapSeconds",
                table: "OS_NOTIFICATIONS_SETTINGS",
                newName: "OS_NTFS_BANNERMINIMUMGAPSECONDS");

            migrationBuilder.RenameColumn(
                name: "AllowShareNotifications",
                table: "OS_NOTIFICATIONS_SETTINGS",
                newName: "OS_NTFS_ALLOWSHARENOTIFICATIONS");

            migrationBuilder.RenameColumn(
                name: "AllowPush",
                table: "OS_NOTIFICATIONS_SETTINGS",
                newName: "OS_NTFS_ALLOWPUSH");

            migrationBuilder.RenameColumn(
                name: "AllowNotifications",
                table: "OS_NOTIFICATIONS_SETTINGS",
                newName: "OS_NTFS_ALLOWNOTIFICATIONS");

            migrationBuilder.RenameColumn(
                name: "AllowMobileBanner",
                table: "OS_NOTIFICATIONS_SETTINGS",
                newName: "OS_NTFS_ALLOWMOBILEBANNER");

            migrationBuilder.RenameColumn(
                name: "AllowEmail",
                table: "OS_NOTIFICATIONS_SETTINGS",
                newName: "OS_NTFS_ALLOWEMAIL");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "OS_NOTIFICATIONS_SETTINGS",
                newName: "OS_NTFS_ID");

            migrationBuilder.RenameIndex(
                name: "IX_NotificationSettings_UserId",
                table: "OS_NOTIFICATIONS_SETTINGS",
                newName: "IX_OS_NOTIFICATIONS_SETTINGS_OS_NTFS_USERID");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "OP_NOTIFICATIONS",
                newName: "OP_NTF_USERID");

            migrationBuilder.RenameColumn(
                name: "Url",
                table: "OP_NOTIFICATIONS",
                newName: "OP_NTF_URL");

            migrationBuilder.RenameColumn(
                name: "TitleArguments",
                table: "OP_NOTIFICATIONS",
                newName: "OP_NTF_TITLEARGUMENTS");

            migrationBuilder.RenameColumn(
                name: "Title",
                table: "OP_NOTIFICATIONS",
                newName: "OP_NTF_TITLE");

            migrationBuilder.RenameColumn(
                name: "ReadAtUtc",
                table: "OP_NOTIFICATIONS",
                newName: "OP_NTF_READATUTC");

            migrationBuilder.RenameColumn(
                name: "Kind",
                table: "OP_NOTIFICATIONS",
                newName: "OP_NTF_KIND");

            migrationBuilder.RenameColumn(
                name: "DismissedAtUtc",
                table: "OP_NOTIFICATIONS",
                newName: "OP_NTF_DISMISSEDATUTC");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "OP_NOTIFICATIONS",
                newName: "OP_NTF_CREATEDATUTC");

            migrationBuilder.RenameColumn(
                name: "BodyArguments",
                table: "OP_NOTIFICATIONS",
                newName: "OP_NTF_BODYARGUMENTS");

            migrationBuilder.RenameColumn(
                name: "Body",
                table: "OP_NOTIFICATIONS",
                newName: "OP_NTF_BODY");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "OP_NOTIFICATIONS",
                newName: "OP_NTF_ID");

            migrationBuilder.RenameIndex(
                name: "IX_NotificationEntries_UserId_CreatedAtUtc",
                table: "OP_NOTIFICATIONS",
                newName: "IX_OP_NOTIFICATIONS_OP_NTF_USERID_OP_NTF_CREATEDATUTC");

            migrationBuilder.RenameColumn(
                name: "SourceNoteId",
                table: "OP_NOTES_SHARED",
                newName: "OP_NS_SOURCENOTEID");

            migrationBuilder.RenameColumn(
                name: "RecipientUserId",
                table: "OP_NOTES_SHARED",
                newName: "OP_NS_RECIPIENTUSERID");

            migrationBuilder.RenameColumn(
                name: "OwnerUserId",
                table: "OP_NOTES_SHARED",
                newName: "OP_NS_OWNERUSERID");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "OP_NOTES_SHARED",
                newName: "OP_NS_CREATEDATUTC");

            migrationBuilder.RenameColumn(
                name: "AccessLevel",
                table: "OP_NOTES_SHARED",
                newName: "OP_NS_ACCESSLEVEL");

            migrationBuilder.RenameColumn(
                name: "AcceptedAtUtc",
                table: "OP_NOTES_SHARED",
                newName: "OP_NS_ACCEPTEDATUTC");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "OP_NOTES_SHARED",
                newName: "OP_NS_ID");

            migrationBuilder.RenameIndex(
                name: "IX_NoteShares_SourceNoteId_RecipientUserId",
                table: "OP_NOTES_SHARED",
                newName: "IX_OP_NOTES_SHARED_OP_NS_SOURCENOTEID_OP_NS_RECIPIENTUSERID");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "OP_NOTES",
                newName: "OP_N_USERID");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "OP_NOTES",
                newName: "OP_N_UPDATEDATUTC");

            migrationBuilder.RenameColumn(
                name: "Title",
                table: "OP_NOTES",
                newName: "OP_N_TITLE");

            migrationBuilder.RenameColumn(
                name: "Priority",
                table: "OP_NOTES",
                newName: "OP_N_PRIORITY");

            migrationBuilder.RenameColumn(
                name: "LockedByUserName",
                table: "OP_NOTES",
                newName: "OP_N_LOCKEDBYUSERNAME");

            migrationBuilder.RenameColumn(
                name: "LockedByUserId",
                table: "OP_NOTES",
                newName: "OP_N_LOCKEDBYUSERID");

            migrationBuilder.RenameColumn(
                name: "LockExpiresAtUtc",
                table: "OP_NOTES",
                newName: "OP_N_LOCKEXPIRESATUTC");

            migrationBuilder.RenameColumn(
                name: "IsPrivate",
                table: "OP_NOTES",
                newName: "OP_N_ISPRIVATE");

            migrationBuilder.RenameColumn(
                name: "IsPinned",
                table: "OP_NOTES",
                newName: "OP_N_ISPINNED");

            migrationBuilder.RenameColumn(
                name: "EncryptedNonce",
                table: "OP_NOTES",
                newName: "OP_N_ENCRYPTEDNONCE");

            migrationBuilder.RenameColumn(
                name: "EncryptedCiphertext",
                table: "OP_NOTES",
                newName: "OP_N_ENCRYPTEDCIPHERTEXT");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "OP_NOTES",
                newName: "OP_N_CREATEDATUTC");

            migrationBuilder.RenameColumn(
                name: "ContentJson",
                table: "OP_NOTES",
                newName: "OP_N_CONTENTJSON");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "OP_NOTES",
                newName: "OP_N_ID");

            migrationBuilder.RenameIndex(
                name: "IX_Notes_UserId",
                table: "OP_NOTES",
                newName: "IX_OP_NOTES_OP_N_USERID");

            migrationBuilder.RenameColumn(
                name: "TaskListId",
                table: "OL_INVENTORIES_TASKS",
                newName: "OL_IT_TASKLISTID");

            migrationBuilder.RenameColumn(
                name: "RefreshTimeOfDayMinutes",
                table: "OL_INVENTORIES_TASKS",
                newName: "OL_IT_REFRESHTIMEOFDAYMINUTES");

            migrationBuilder.RenameColumn(
                name: "OnlyLinkedWithDueDate",
                table: "OL_INVENTORIES_TASKS",
                newName: "OL_IT_ONLYLINKEDWITHDUEDATE");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "OL_INVENTORIES_TASKS",
                newName: "OL_IT_ID");

            migrationBuilder.RenameColumn(
                name: "WarehouseId",
                table: "OL_INVENTORIES_TASKS",
                newName: "OL_IT_INVENTORYID");

            migrationBuilder.RenameIndex(
                name: "IX_InventoryManagedTaskLists_WarehouseId",
                table: "OL_INVENTORIES_TASKS",
                newName: "IX_OL_INVENTORIES_TASKS_OL_IT_INVENTORYID");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "OP_INVENTORIES_ITEMS",
                newName: "OP_II_UPDATEDATUTC");

            migrationBuilder.RenameColumn(
                name: "Unit",
                table: "OP_INVENTORIES_ITEMS",
                newName: "OP_II_UNIT");

            migrationBuilder.RenameColumn(
                name: "Quantity",
                table: "OP_INVENTORIES_ITEMS",
                newName: "OP_II_QUANTITY");

            migrationBuilder.RenameColumn(
                name: "ProductType",
                table: "OP_INVENTORIES_ITEMS",
                newName: "OP_II_PRODUCTTYPE");

            migrationBuilder.RenameColumn(
                name: "Position",
                table: "OP_INVENTORIES_ITEMS",
                newName: "OP_II_POSITION");

            migrationBuilder.RenameColumn(
                name: "PendingRestockTaskListId",
                table: "OP_INVENTORIES_ITEMS",
                newName: "OP_II_PENDINGRESTOCKTASKLISTID");

            migrationBuilder.RenameColumn(
                name: "PendingRestockTaskItemId",
                table: "OP_INVENTORIES_ITEMS",
                newName: "OP_II_PENDINGRESTOCKTASKITEMID");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "OP_INVENTORIES_ITEMS",
                newName: "OP_II_NAME");

            migrationBuilder.RenameColumn(
                name: "MinimumQuantity",
                table: "OP_INVENTORIES_ITEMS",
                newName: "OP_II_MINIMUMQUANTITY");

            migrationBuilder.RenameColumn(
                name: "IsCheckedRegularly",
                table: "OP_INVENTORIES_ITEMS",
                newName: "OP_II_ISCHECKEDREGULARLY");

            migrationBuilder.RenameColumn(
                name: "ExpiryNotificationChannel",
                table: "OP_INVENTORIES_ITEMS",
                newName: "OP_II_EXPIRYNOTIFICATIONCHANNEL");

            migrationBuilder.RenameColumn(
                name: "ExpiryDate",
                table: "OP_INVENTORIES_ITEMS",
                newName: "OP_II_EXPIRYDATE");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "OP_INVENTORIES_ITEMS",
                newName: "OP_II_CREATEDATUTC");

            migrationBuilder.RenameColumn(
                name: "Category",
                table: "OP_INVENTORIES_ITEMS",
                newName: "OP_II_CATEGORY");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "OP_INVENTORIES_ITEMS",
                newName: "OP_II_ID");

            migrationBuilder.RenameColumn(
                name: "WarehouseId",
                table: "OP_INVENTORIES_ITEMS",
                newName: "OP_II_INVENTORYID");

            migrationBuilder.RenameColumn(
                name: "SentAtUtc",
                table: "OS_INVENTORIES_EXPIRY",
                newName: "OS_IE_SENTATUTC");

            migrationBuilder.RenameColumn(
                name: "InventoryItemId",
                table: "OS_INVENTORIES_EXPIRY",
                newName: "OS_IE_INVENTORYITEMID");

            migrationBuilder.RenameColumn(
                name: "ExpiryDate",
                table: "OS_INVENTORIES_EXPIRY",
                newName: "OS_IE_EXPIRYDATE");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "OS_INVENTORIES_EXPIRY",
                newName: "OS_IE_ID");

            migrationBuilder.RenameIndex(
                name: "IX_InventoryExpiryNotificationDeliveries_InventoryItemId_Expir~",
                table: "OS_INVENTORIES_EXPIRY",
                newName: "IX_OS_INVENTORIES_EXPIRY_OS_IE_INVENTORYITEMID_OS_IE_EXPIRYDATE");

            migrationBuilder.RenameColumn(
                name: "SentAtUtc",
                table: "OS_EVENTS_REMINDERS",
                newName: "OS_ER_SENTATUTC");

            migrationBuilder.RenameColumn(
                name: "OccurrenceStartUtc",
                table: "OS_EVENTS_REMINDERS",
                newName: "OS_ER_OCCURRENCESTARTUTC");

            migrationBuilder.RenameColumn(
                name: "MinutesBeforeStart",
                table: "OS_EVENTS_REMINDERS",
                newName: "OS_ER_MINUTESBEFORESTART");

            migrationBuilder.RenameColumn(
                name: "CalendarEventId",
                table: "OS_EVENTS_REMINDERS",
                newName: "OS_ER_CALENDAREVENTID");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "OS_EVENTS_REMINDERS",
                newName: "OS_ER_ID");

            migrationBuilder.RenameIndex(
                name: "IX_EventReminderDeliveries_CalendarEventId_MinutesBeforeStart_~",
                table: "OS_EVENTS_REMINDERS",
                newName: "IX_OS_EVENTS_REMINDERS_OS_ER_CALENDAREVENTID_OS_ER_MINUTESBEFO~");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "OS_DIAGNOSTICS",
                newName: "OS_D_USERID");

            migrationBuilder.RenameColumn(
                name: "TimestampUtc",
                table: "OS_DIAGNOSTICS",
                newName: "OS_D_TIMESTAMPUTC");

            migrationBuilder.RenameColumn(
                name: "ReceivedAtUtc",
                table: "OS_DIAGNOSTICS",
                newName: "OS_D_RECEIVEDATUTC");

            migrationBuilder.RenameColumn(
                name: "Platform",
                table: "OS_DIAGNOSTICS",
                newName: "OS_D_PLATFORM");

            migrationBuilder.RenameColumn(
                name: "OperatingSystemVersion",
                table: "OS_DIAGNOSTICS",
                newName: "OS_D_OPERATINGSYSTEMVERSION");

            migrationBuilder.RenameColumn(
                name: "Message",
                table: "OS_DIAGNOSTICS",
                newName: "OS_D_MESSAGE");

            migrationBuilder.RenameColumn(
                name: "Level",
                table: "OS_DIAGNOSTICS",
                newName: "OS_D_LEVEL");

            migrationBuilder.RenameColumn(
                name: "DeviceModel",
                table: "OS_DIAGNOSTICS",
                newName: "OS_D_DEVICEMODEL");

            migrationBuilder.RenameColumn(
                name: "Detail",
                table: "OS_DIAGNOSTICS",
                newName: "OS_D_DETAIL");

            migrationBuilder.RenameColumn(
                name: "AppVersion",
                table: "OS_DIAGNOSTICS",
                newName: "OS_D_APPVERSION");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "OS_DIAGNOSTICS",
                newName: "OS_D_ID");

            migrationBuilder.RenameIndex(
                name: "IX_DiagnosticLogEntries_UserId_ReceivedAtUtc",
                table: "OS_DIAGNOSTICS",
                newName: "IX_OS_DIAGNOSTICS_OS_D_USERID_OS_D_RECEIVEDATUTC");

            migrationBuilder.RenameIndex(
                name: "IX_DiagnosticLogEntries_ReceivedAtUtc",
                table: "OS_DIAGNOSTICS",
                newName: "IX_OS_DIAGNOSTICS_OS_D_RECEIVEDATUTC");

            migrationBuilder.RenameColumn(
                name: "OwnerUserId",
                table: "OL_CONTACTS",
                newName: "OL_C_OWNERUSERID");

            migrationBuilder.RenameColumn(
                name: "LastMessageAtUtc",
                table: "OL_CONTACTS",
                newName: "OL_C_LASTMESSAGEATUTC");

            migrationBuilder.RenameColumn(
                name: "IsArchived",
                table: "OL_CONTACTS",
                newName: "OL_C_ISARCHIVED");

            migrationBuilder.RenameColumn(
                name: "HistoryClearedAtUtc",
                table: "OL_CONTACTS",
                newName: "OL_C_HISTORYCLEAREDATUTC");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "OL_CONTACTS",
                newName: "OL_C_CREATEDATUTC");

            migrationBuilder.RenameColumn(
                name: "ContactUserId",
                table: "OL_CONTACTS",
                newName: "OL_C_CONTACTUSERID");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "OL_CONTACTS",
                newName: "OL_C_ID");

            migrationBuilder.RenameIndex(
                name: "IX_Contacts_OwnerUserId_ContactUserId",
                table: "OL_CONTACTS",
                newName: "IX_OL_CONTACTS_OL_C_OWNERUSERID_OL_C_CONTACTUSERID");

            migrationBuilder.RenameColumn(
                name: "SentAtUtc",
                table: "OP_CHATS",
                newName: "OP_C_SENTATUTC");

            migrationBuilder.RenameColumn(
                name: "SenderUserId",
                table: "OP_CHATS",
                newName: "OP_C_SENDERUSERID");

            migrationBuilder.RenameColumn(
                name: "RecipientUserId",
                table: "OP_CHATS",
                newName: "OP_C_RECIPIENTUSERID");

            migrationBuilder.RenameColumn(
                name: "ReadAtUtc",
                table: "OP_CHATS",
                newName: "OP_C_READATUTC");

            migrationBuilder.RenameColumn(
                name: "NonceBase64",
                table: "OP_CHATS",
                newName: "OP_C_NONCEBASE64");

            migrationBuilder.RenameColumn(
                name: "IsSharedHistory",
                table: "OP_CHATS",
                newName: "OP_C_ISSHAREDHISTORY");

            migrationBuilder.RenameColumn(
                name: "IsEdited",
                table: "OP_CHATS",
                newName: "OP_C_ISEDITED");

            migrationBuilder.RenameColumn(
                name: "GroupMessageId",
                table: "OP_CHATS",
                newName: "OP_C_GROUPMESSAGEID");

            migrationBuilder.RenameColumn(
                name: "GroupId",
                table: "OP_CHATS",
                newName: "OP_C_GROUPID");

            migrationBuilder.RenameColumn(
                name: "EditedAtUtc",
                table: "OP_CHATS",
                newName: "OP_C_EDITEDATUTC");

            migrationBuilder.RenameColumn(
                name: "CiphertextBase64",
                table: "OP_CHATS",
                newName: "OP_C_CIPHERTEXTBASE64");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "OP_CHATS",
                newName: "OP_C_ID");

            migrationBuilder.RenameIndex(
                name: "IX_ChatMessages_SenderUserId_RecipientUserId",
                table: "OP_CHATS",
                newName: "IX_OP_CHATS_OP_C_SENDERUSERID_OP_C_RECIPIENTUSERID");

            migrationBuilder.RenameIndex(
                name: "IX_ChatMessages_RecipientUserId_SenderUserId",
                table: "OP_CHATS",
                newName: "IX_OP_CHATS_OP_C_RECIPIENTUSERID_OP_C_SENDERUSERID");

            migrationBuilder.RenameIndex(
                name: "IX_ChatMessages_GroupMessageId",
                table: "OP_CHATS",
                newName: "IX_OP_CHATS_OP_C_GROUPMESSAGEID");

            migrationBuilder.RenameIndex(
                name: "IX_ChatMessages_GroupId",
                table: "OP_CHATS",
                newName: "IX_OP_CHATS_OP_C_GROUPID");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "OP_CHATS_GROUPS",
                newName: "OP_CG_NAME");

            migrationBuilder.RenameColumn(
                name: "LastMessageAtUtc",
                table: "OP_CHATS_GROUPS",
                newName: "OP_CG_LASTMESSAGEATUTC");

            migrationBuilder.RenameColumn(
                name: "CreatedByUserId",
                table: "OP_CHATS_GROUPS",
                newName: "OP_CG_CREATEDBYUSERID");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "OP_CHATS_GROUPS",
                newName: "OP_CG_CREATEDATUTC");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "OP_CHATS_GROUPS",
                newName: "OP_CG_ID");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "OL_CHATS_MEMBERS",
                newName: "OL_CM_USERID");

            migrationBuilder.RenameColumn(
                name: "Role",
                table: "OL_CHATS_MEMBERS",
                newName: "OL_CM_ROLE");

            migrationBuilder.RenameColumn(
                name: "JoinedAtUtc",
                table: "OL_CHATS_MEMBERS",
                newName: "OL_CM_JOINEDATUTC");

            migrationBuilder.RenameColumn(
                name: "IsArchived",
                table: "OL_CHATS_MEMBERS",
                newName: "OL_CM_ISARCHIVED");

            migrationBuilder.RenameColumn(
                name: "GroupId",
                table: "OL_CHATS_MEMBERS",
                newName: "OL_CM_GROUPID");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "OL_CHATS_MEMBERS",
                newName: "OL_CM_ID");

            migrationBuilder.RenameIndex(
                name: "IX_ChatGroupMembers_UserId",
                table: "OL_CHATS_MEMBERS",
                newName: "IX_OL_CHATS_MEMBERS_OL_CM_USERID");

            migrationBuilder.RenameIndex(
                name: "IX_ChatGroupMembers_GroupId_UserId",
                table: "OL_CHATS_MEMBERS",
                newName: "IX_OL_CHATS_MEMBERS_OL_CM_GROUPID_OL_CM_USERID");

            migrationBuilder.RenameColumn(
                name: "JoinedUserId",
                table: "OP_CHATS_ANNOUNCEMENTS",
                newName: "OP_CA_JOINEDUSERID");

            migrationBuilder.RenameColumn(
                name: "HistoryShared",
                table: "OP_CHATS_ANNOUNCEMENTS",
                newName: "OP_CA_HISTORYSHARED");

            migrationBuilder.RenameColumn(
                name: "GroupId",
                table: "OP_CHATS_ANNOUNCEMENTS",
                newName: "OP_CA_GROUPID");

            migrationBuilder.RenameColumn(
                name: "AnnouncedAtUtc",
                table: "OP_CHATS_ANNOUNCEMENTS",
                newName: "OP_CA_ANNOUNCEDATUTC");

            migrationBuilder.RenameColumn(
                name: "AddedByUserId",
                table: "OP_CHATS_ANNOUNCEMENTS",
                newName: "OP_CA_ADDEDBYUSERID");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "OP_CHATS_ANNOUNCEMENTS",
                newName: "OP_CA_ID");

            migrationBuilder.RenameIndex(
                name: "IX_ChatGroupAnnouncements_GroupId_JoinedUserId",
                table: "OP_CHATS_ANNOUNCEMENTS",
                newName: "IX_OP_CHATS_ANNOUNCEMENTS_OP_CA_GROUPID_OP_CA_JOINEDUSERID");

            migrationBuilder.RenameColumn(
                name: "OtherUserId",
                table: "OL_CHATS_ACCESS",
                newName: "OL_CA_OTHERUSERID");

            migrationBuilder.RenameColumn(
                name: "InitiatedByUserId",
                table: "OL_CHATS_ACCESS",
                newName: "OL_CA_INITIATEDBYUSERID");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "OL_CHATS_ACCESS",
                newName: "OL_CA_CREATEDATUTC");

            migrationBuilder.RenameColumn(
                name: "ApprovedAtUtc",
                table: "OL_CHATS_ACCESS",
                newName: "OL_CA_APPROVEDATUTC");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "OL_CHATS_ACCESS",
                newName: "OL_CA_ID");

            migrationBuilder.RenameIndex(
                name: "IX_ChatConversationAccesses_InitiatedByUserId_OtherUserId",
                table: "OL_CHATS_ACCESS",
                newName: "IX_OL_CHATS_ACCESS_OL_CA_INITIATEDBYUSERID_OL_CA_OTHERUSERID");

            migrationBuilder.RenameColumn(
                name: "SourceCalendarEventId",
                table: "OP_EVENTS_SHARED",
                newName: "OP_ES_SOURCECALENDAREVENTID");

            migrationBuilder.RenameColumn(
                name: "RecipientUserId",
                table: "OP_EVENTS_SHARED",
                newName: "OP_ES_RECIPIENTUSERID");

            migrationBuilder.RenameColumn(
                name: "OwnerUserId",
                table: "OP_EVENTS_SHARED",
                newName: "OP_ES_OWNERUSERID");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "OP_EVENTS_SHARED",
                newName: "OP_ES_CREATEDATUTC");

            migrationBuilder.RenameColumn(
                name: "AccessLevel",
                table: "OP_EVENTS_SHARED",
                newName: "OP_ES_ACCESSLEVEL");

            migrationBuilder.RenameColumn(
                name: "AcceptedAtUtc",
                table: "OP_EVENTS_SHARED",
                newName: "OP_ES_ACCEPTEDATUTC");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "OP_EVENTS_SHARED",
                newName: "OP_ES_ID");

            migrationBuilder.RenameIndex(
                name: "IX_CalendarEventShares_SourceCalendarEventId_RecipientUserId",
                table: "OP_EVENTS_SHARED",
                newName: "IX_OP_EVENTS_SHARED_OP_ES_SOURCECALENDAREVENTID_OP_ES_RECIPIEN~");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "OP_EVENTS",
                newName: "OP_E_USERID");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "OP_EVENTS",
                newName: "OP_E_UPDATEDATUTC");

            migrationBuilder.RenameColumn(
                name: "Title",
                table: "OP_EVENTS",
                newName: "OP_E_TITLE");

            migrationBuilder.RenameColumn(
                name: "StartUtc",
                table: "OP_EVENTS",
                newName: "OP_E_STARTUTC");

            migrationBuilder.RenameColumn(
                name: "RemindersJson",
                table: "OP_EVENTS",
                newName: "OP_E_REMINDERSJSON");

            migrationBuilder.RenameColumn(
                name: "ReminderNotificationChannel",
                table: "OP_EVENTS",
                newName: "OP_E_REMINDERNOTIFICATIONCHANNEL");

            migrationBuilder.RenameColumn(
                name: "RecurrenceUntilUtc",
                table: "OP_EVENTS",
                newName: "OP_E_RECURRENCEUNTILUTC");

            migrationBuilder.RenameColumn(
                name: "RecurrenceOccurrenceCount",
                table: "OP_EVENTS",
                newName: "OP_E_RECURRENCEOCCURRENCECOUNT");

            migrationBuilder.RenameColumn(
                name: "RecurrenceIntervalCount",
                table: "OP_EVENTS",
                newName: "OP_E_RECURRENCEINTERVALCOUNT");

            migrationBuilder.RenameColumn(
                name: "RecurrenceFrequency",
                table: "OP_EVENTS",
                newName: "OP_E_RECURRENCEFREQUENCY");

            migrationBuilder.RenameColumn(
                name: "Priority",
                table: "OP_EVENTS",
                newName: "OP_E_PRIORITY");

            migrationBuilder.RenameColumn(
                name: "NotifyAtStart",
                table: "OP_EVENTS",
                newName: "OP_E_NOTIFYATSTART");

            migrationBuilder.RenameColumn(
                name: "LockedByUserName",
                table: "OP_EVENTS",
                newName: "OP_E_LOCKEDBYUSERNAME");

            migrationBuilder.RenameColumn(
                name: "LockedByUserId",
                table: "OP_EVENTS",
                newName: "OP_E_LOCKEDBYUSERID");

            migrationBuilder.RenameColumn(
                name: "LockExpiresAtUtc",
                table: "OP_EVENTS",
                newName: "OP_E_LOCKEXPIRESATUTC");

            migrationBuilder.RenameColumn(
                name: "LocationLongitude",
                table: "OP_EVENTS",
                newName: "OP_E_LOCATIONLONGITUDE");

            migrationBuilder.RenameColumn(
                name: "LocationLatitude",
                table: "OP_EVENTS",
                newName: "OP_E_LOCATIONLATITUDE");

            migrationBuilder.RenameColumn(
                name: "LocationAddress",
                table: "OP_EVENTS",
                newName: "OP_E_LOCATIONADDRESS");

            migrationBuilder.RenameColumn(
                name: "IsAllDay",
                table: "OP_EVENTS",
                newName: "OP_E_ISALLDAY");

            migrationBuilder.RenameColumn(
                name: "GuestsJson",
                table: "OP_EVENTS",
                newName: "OP_E_GUESTSJSON");

            migrationBuilder.RenameColumn(
                name: "EndUtc",
                table: "OP_EVENTS",
                newName: "OP_E_ENDUTC");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "OP_EVENTS",
                newName: "OP_E_DESCRIPTION");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "OP_EVENTS",
                newName: "OP_E_CREATEDATUTC");

            migrationBuilder.RenameColumn(
                name: "Color",
                table: "OP_EVENTS",
                newName: "OP_E_COLOR");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "OP_EVENTS",
                newName: "OP_E_ID");

            migrationBuilder.RenameIndex(
                name: "IX_CalendarEvents_UserId",
                table: "OP_EVENTS",
                newName: "IX_OP_EVENTS_OP_E_USERID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OS_VERIFICATION_CODES",
                table: "OS_VERIFICATION_CODES",
                column: "OS_VC_ID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OS_USERS",
                table: "OS_USERS",
                column: "OS_U_ID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OS_USERS_PERMISSIONS",
                table: "OS_USERS_PERMISSIONS",
                columns: new[] { "OS_UP_USERID", "OS_UP_PERMISSION" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_OP_TASKS_SHARED",
                table: "OP_TASKS_SHARED",
                column: "OP_TS_ID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OP_TASKS",
                table: "OP_TASKS",
                column: "OP_T_ID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OS_TASKS_OVERDUE",
                table: "OS_TASKS_OVERDUE",
                column: "OS_TO_ID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OL_TASKS_ITEMS",
                table: "OL_TASKS_ITEMS",
                columns: new[] { "OL_TI_TASKITEMID", "OL_TI_LINKEDTASKLISTID" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_OP_TASKS_ITEMS",
                table: "OP_TASKS_ITEMS",
                column: "OP_TI_ID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OP_TASKS_CATEGORIES",
                table: "OP_TASKS_CATEGORIES",
                columns: new[] { "OP_TC_TASKITEMID", "OP_TC_CATEGORY" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_OS_TASKS_REMINDERS",
                table: "OS_TASKS_REMINDERS",
                column: "OS_TR_ID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OS_SYNC_TOMBSTONES",
                table: "OS_SYNC_TOMBSTONES",
                column: "OS_ST_ID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OP_LOCATIONS",
                table: "OP_LOCATIONS",
                column: "OP_L_ID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OS_REFRESH_TOKENS",
                table: "OS_REFRESH_TOKENS",
                column: "OS_RT_ID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OS_PUSH_SUBSCRIPTIONS",
                table: "OS_PUSH_SUBSCRIPTIONS",
                column: "OS_PS_ID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OL_PUBLIC_SHARES",
                table: "OL_PUBLIC_SHARES",
                column: "OL_PS_ID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OS_PERMISSIONS_CODES",
                table: "OS_PERMISSIONS_CODES",
                column: "OS_PC_PERMISSION");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OS_NOTIFICATIONS_SETTINGS",
                table: "OS_NOTIFICATIONS_SETTINGS",
                column: "OS_NTFS_ID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OP_NOTIFICATIONS",
                table: "OP_NOTIFICATIONS",
                column: "OP_NTF_ID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OP_NOTES_SHARED",
                table: "OP_NOTES_SHARED",
                column: "OP_NS_ID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OP_NOTES",
                table: "OP_NOTES",
                column: "OP_N_ID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OL_INVENTORIES_TASKS",
                table: "OL_INVENTORIES_TASKS",
                column: "OL_IT_ID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OS_INVENTORIES_EXPIRY",
                table: "OS_INVENTORIES_EXPIRY",
                column: "OS_IE_ID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OS_EVENTS_REMINDERS",
                table: "OS_EVENTS_REMINDERS",
                column: "OS_ER_ID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OS_DIAGNOSTICS",
                table: "OS_DIAGNOSTICS",
                column: "OS_D_ID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OL_CONTACTS",
                table: "OL_CONTACTS",
                column: "OL_C_ID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OP_CHATS",
                table: "OP_CHATS",
                column: "OP_C_ID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OP_CHATS_GROUPS",
                table: "OP_CHATS_GROUPS",
                column: "OP_CG_ID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OL_CHATS_MEMBERS",
                table: "OL_CHATS_MEMBERS",
                column: "OL_CM_ID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OP_CHATS_ANNOUNCEMENTS",
                table: "OP_CHATS_ANNOUNCEMENTS",
                column: "OP_CA_ID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OL_CHATS_ACCESS",
                table: "OL_CHATS_ACCESS",
                column: "OL_CA_ID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OP_EVENTS_SHARED",
                table: "OP_EVENTS_SHARED",
                column: "OP_ES_ID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OP_EVENTS",
                table: "OP_EVENTS",
                column: "OP_E_ID");

            migrationBuilder.AddForeignKey(
                name: "FK_OL_CHATS_MEMBERS_OP_CHATS_GROUPS_OL_CM_GROUPID",
                table: "OL_CHATS_MEMBERS",
                column: "OL_CM_GROUPID",
                principalTable: "OP_CHATS_GROUPS",
                principalColumn: "OP_CG_ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OL_TASKS_ITEMS_OP_TASKS_ITEMS_OL_TI_TASKITEMID",
                table: "OL_TASKS_ITEMS",
                column: "OL_TI_TASKITEMID",
                principalTable: "OP_TASKS_ITEMS",
                principalColumn: "OP_TI_ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OP_CHATS_ANNOUNCEMENTS_OP_CHATS_GROUPS_OP_CA_GROUPID",
                table: "OP_CHATS_ANNOUNCEMENTS",
                column: "OP_CA_GROUPID",
                principalTable: "OP_CHATS_GROUPS",
                principalColumn: "OP_CG_ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OP_TASKS_CATEGORIES_OP_TASKS_ITEMS_OP_TC_TASKITEMID",
                table: "OP_TASKS_CATEGORIES",
                column: "OP_TC_TASKITEMID",
                principalTable: "OP_TASKS_ITEMS",
                principalColumn: "OP_TI_ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OP_TASKS_ITEMS_OP_TASKS_OP_TI_TASKID",
                table: "OP_TASKS_ITEMS",
                column: "OP_TI_TASKID",
                principalTable: "OP_TASKS",
                principalColumn: "OP_T_ID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OL_CHATS_MEMBERS_OP_CHATS_GROUPS_OL_CM_GROUPID",
                table: "OL_CHATS_MEMBERS");

            migrationBuilder.DropForeignKey(
                name: "FK_OL_TASKS_ITEMS_OP_TASKS_ITEMS_OL_TI_TASKITEMID",
                table: "OL_TASKS_ITEMS");

            migrationBuilder.DropForeignKey(
                name: "FK_OP_CHATS_ANNOUNCEMENTS_OP_CHATS_GROUPS_OP_CA_GROUPID",
                table: "OP_CHATS_ANNOUNCEMENTS");

            migrationBuilder.DropForeignKey(
                name: "FK_OP_TASKS_CATEGORIES_OP_TASKS_ITEMS_OP_TC_TASKITEMID",
                table: "OP_TASKS_CATEGORIES");

            migrationBuilder.DropForeignKey(
                name: "FK_OP_TASKS_ITEMS_OP_TASKS_OP_TI_TASKID",
                table: "OP_TASKS_ITEMS");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OS_VERIFICATION_CODES",
                table: "OS_VERIFICATION_CODES");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OS_USERS_PERMISSIONS",
                table: "OS_USERS_PERMISSIONS");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OS_USERS",
                table: "OS_USERS");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OS_TASKS_REMINDERS",
                table: "OS_TASKS_REMINDERS");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OS_TASKS_OVERDUE",
                table: "OS_TASKS_OVERDUE");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OS_SYNC_TOMBSTONES",
                table: "OS_SYNC_TOMBSTONES");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OS_REFRESH_TOKENS",
                table: "OS_REFRESH_TOKENS");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OS_PUSH_SUBSCRIPTIONS",
                table: "OS_PUSH_SUBSCRIPTIONS");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OS_PERMISSIONS_CODES",
                table: "OS_PERMISSIONS_CODES");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OS_NOTIFICATIONS_SETTINGS",
                table: "OS_NOTIFICATIONS_SETTINGS");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OS_INVENTORIES_EXPIRY",
                table: "OS_INVENTORIES_EXPIRY");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OS_EVENTS_REMINDERS",
                table: "OS_EVENTS_REMINDERS");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OS_DIAGNOSTICS",
                table: "OS_DIAGNOSTICS");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OP_TASKS_SHARED",
                table: "OP_TASKS_SHARED");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OP_TASKS_ITEMS",
                table: "OP_TASKS_ITEMS");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OP_TASKS_CATEGORIES",
                table: "OP_TASKS_CATEGORIES");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OP_TASKS",
                table: "OP_TASKS");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OP_NOTIFICATIONS",
                table: "OP_NOTIFICATIONS");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OP_NOTES_SHARED",
                table: "OP_NOTES_SHARED");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OP_NOTES",
                table: "OP_NOTES");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OP_LOCATIONS",
                table: "OP_LOCATIONS");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OP_EVENTS_SHARED",
                table: "OP_EVENTS_SHARED");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OP_EVENTS",
                table: "OP_EVENTS");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OP_CHATS_GROUPS",
                table: "OP_CHATS_GROUPS");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OP_CHATS_ANNOUNCEMENTS",
                table: "OP_CHATS_ANNOUNCEMENTS");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OP_CHATS",
                table: "OP_CHATS");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OL_TASKS_ITEMS",
                table: "OL_TASKS_ITEMS");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OL_PUBLIC_SHARES",
                table: "OL_PUBLIC_SHARES");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OL_INVENTORIES_TASKS",
                table: "OL_INVENTORIES_TASKS");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OL_CONTACTS",
                table: "OL_CONTACTS");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OL_CHATS_MEMBERS",
                table: "OL_CHATS_MEMBERS");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OL_CHATS_ACCESS",
                table: "OL_CHATS_ACCESS");

            migrationBuilder.RenameTable(
                name: "OS_VERIFICATION_CODES",
                newName: "UserVerificationCodes");

            migrationBuilder.RenameTable(
                name: "OS_USERS_PERMISSIONS",
                newName: "UserPermissions");

            migrationBuilder.RenameTable(
                name: "OS_USERS",
                newName: "Users");

            migrationBuilder.RenameTable(
                name: "OS_TASKS_REMINDERS",
                newName: "TaskDailyReminderDeliveries");

            migrationBuilder.RenameTable(
                name: "OS_TASKS_OVERDUE",
                newName: "TaskOverdueNotificationDeliveries");

            migrationBuilder.RenameTable(
                name: "OS_SYNC_TOMBSTONES",
                newName: "SyncTombstones");

            migrationBuilder.RenameTable(
                name: "OS_REFRESH_TOKENS",
                newName: "RefreshTokens");

            migrationBuilder.RenameTable(
                name: "OS_PUSH_SUBSCRIPTIONS",
                newName: "PushSubscriptions");

            migrationBuilder.RenameTable(
                name: "OS_PERMISSIONS_CODES",
                newName: "PermissionCodes");

            migrationBuilder.RenameTable(
                name: "OS_NOTIFICATIONS_SETTINGS",
                newName: "NotificationSettings");

            migrationBuilder.RenameTable(
                name: "OS_INVENTORIES_EXPIRY",
                newName: "InventoryExpiryNotificationDeliveries");

            migrationBuilder.RenameTable(
                name: "OS_EVENTS_REMINDERS",
                newName: "EventReminderDeliveries");

            migrationBuilder.RenameTable(
                name: "OS_DIAGNOSTICS",
                newName: "DiagnosticLogEntries");

            migrationBuilder.RenameTable(
                name: "OP_TASKS_SHARED",
                newName: "TaskShares");

            migrationBuilder.RenameTable(
                name: "OP_TASKS_ITEMS",
                newName: "TaskItemEntity");

            migrationBuilder.RenameTable(
                name: "OP_TASKS_CATEGORIES",
                newName: "TaskItemCategoryEntity");

            migrationBuilder.RenameTable(
                name: "OP_TASKS",
                newName: "Tasks");

            migrationBuilder.RenameTable(
                name: "OP_NOTIFICATIONS",
                newName: "NotificationEntries");

            migrationBuilder.RenameTable(
                name: "OP_NOTES_SHARED",
                newName: "NoteShares");

            migrationBuilder.RenameTable(
                name: "OP_NOTES",
                newName: "Notes");

            migrationBuilder.RenameTable(
                name: "OP_LOCATIONS",
                newName: "SharedLocations");

            migrationBuilder.RenameTable(
                name: "OP_INVENTORIES_ITEMS",
                newName: "InventoryItems");

            migrationBuilder.RenameTable(
                name: "OP_EVENTS_SHARED",
                newName: "CalendarEventShares");

            migrationBuilder.RenameTable(
                name: "OP_EVENTS",
                newName: "CalendarEvents");

            migrationBuilder.RenameTable(
                name: "OP_CHATS_GROUPS",
                newName: "ChatGroups");

            migrationBuilder.RenameTable(
                name: "OP_CHATS_ANNOUNCEMENTS",
                newName: "ChatGroupAnnouncements");

            migrationBuilder.RenameTable(
                name: "OP_CHATS",
                newName: "ChatMessages");

            migrationBuilder.RenameTable(
                name: "OL_TASKS_ITEMS",
                newName: "TaskItemTaskListLinkEntity");

            migrationBuilder.RenameTable(
                name: "OL_PUBLIC_SHARES",
                newName: "PublicShareLinks");

            migrationBuilder.RenameTable(
                name: "OL_INVENTORIES_TASKS",
                newName: "InventoryManagedTaskLists");

            migrationBuilder.RenameTable(
                name: "OL_CONTACTS",
                newName: "Contacts");

            migrationBuilder.RenameTable(
                name: "OL_CHATS_MEMBERS",
                newName: "ChatGroupMembers");

            migrationBuilder.RenameTable(
                name: "OL_CHATS_ACCESS",
                newName: "ChatConversationAccesses");

            migrationBuilder.RenameColumn(
                name: "OS_VC_USERID",
                table: "UserVerificationCodes",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "OS_VC_PURPOSE",
                table: "UserVerificationCodes",
                newName: "Purpose");

            migrationBuilder.RenameColumn(
                name: "OS_VC_FAILEDATTEMPTS",
                table: "UserVerificationCodes",
                newName: "FailedAttempts");

            migrationBuilder.RenameColumn(
                name: "OS_VC_EXPIRESATUTC",
                table: "UserVerificationCodes",
                newName: "ExpiresAtUtc");

            migrationBuilder.RenameColumn(
                name: "OS_VC_EMAILADDRESS",
                table: "UserVerificationCodes",
                newName: "EmailAddress");

            migrationBuilder.RenameColumn(
                name: "OS_VC_CREATEDATUTC",
                table: "UserVerificationCodes",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OS_VC_CONSUMEDATUTC",
                table: "UserVerificationCodes",
                newName: "ConsumedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OS_VC_CODEHASH",
                table: "UserVerificationCodes",
                newName: "CodeHash");

            migrationBuilder.RenameColumn(
                name: "OS_VC_ID",
                table: "UserVerificationCodes",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_OS_VERIFICATION_CODES_OS_VC_USERID_OS_VC_PURPOSE",
                table: "UserVerificationCodes",
                newName: "IX_UserVerificationCodes_UserId_Purpose");

            migrationBuilder.RenameColumn(
                name: "OS_UP_GRANTEDATUTC",
                table: "UserPermissions",
                newName: "GrantedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OS_UP_PERMISSION",
                table: "UserPermissions",
                newName: "Permission");

            migrationBuilder.RenameColumn(
                name: "OS_UP_USERID",
                table: "UserPermissions",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "OS_U_WRAPPEDPRIVATEKEYBASE64",
                table: "Users",
                newName: "WrappedPrivateKeyBase64");

            migrationBuilder.RenameColumn(
                name: "OS_U_USERNAME",
                table: "Users",
                newName: "UserName");

            migrationBuilder.RenameColumn(
                name: "OS_U_PUBLICKEYBASE64",
                table: "Users",
                newName: "PublicKeyBase64");

            migrationBuilder.RenameColumn(
                name: "OS_U_PRIVATEKEYWRAPNONCEBASE64",
                table: "Users",
                newName: "PrivateKeyWrapNonceBase64");

            migrationBuilder.RenameColumn(
                name: "OS_U_PRIVATEKEYSALTBASE64",
                table: "Users",
                newName: "PrivateKeySaltBase64");

            migrationBuilder.RenameColumn(
                name: "OS_U_PRIVATEKEYDERIVATIONITERATIONS",
                table: "Users",
                newName: "PrivateKeyDerivationIterations");

            migrationBuilder.RenameColumn(
                name: "OS_U_PRESENCELASTSEENATUTC",
                table: "Users",
                newName: "PresenceLastSeenAtUtc");

            migrationBuilder.RenameColumn(
                name: "OS_U_PRESENCEAVAILABILITY",
                table: "Users",
                newName: "PresenceAvailability");

            migrationBuilder.RenameColumn(
                name: "OS_U_PASSWORDHASH",
                table: "Users",
                newName: "PasswordHash");

            migrationBuilder.RenameColumn(
                name: "OS_U_LOCATIONRECORDEDATUTC",
                table: "Users",
                newName: "LocationRecordedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OS_U_LOCATIONLONGITUDE",
                table: "Users",
                newName: "LocationLongitude");

            migrationBuilder.RenameColumn(
                name: "OS_U_LOCATIONLATITUDE",
                table: "Users",
                newName: "LocationLatitude");

            migrationBuilder.RenameColumn(
                name: "OS_U_LOCATIONADDRESS",
                table: "Users",
                newName: "LocationAddress");

            migrationBuilder.RenameColumn(
                name: "OS_U_GOOGLESUBJECTID",
                table: "Users",
                newName: "GoogleSubjectId");

            migrationBuilder.RenameColumn(
                name: "OS_U_EMAILVERIFIEDATUTC",
                table: "Users",
                newName: "EmailVerifiedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OS_U_EMAIL",
                table: "Users",
                newName: "Email");

            migrationBuilder.RenameColumn(
                name: "OS_U_DISPLAYNAME",
                table: "Users",
                newName: "DisplayName");

            migrationBuilder.RenameColumn(
                name: "OS_U_CREATEDATUTC",
                table: "Users",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OS_U_ID",
                table: "Users",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_OS_USERS_OS_U_USERNAME",
                table: "Users",
                newName: "IX_Users_UserName");

            migrationBuilder.RenameIndex(
                name: "IX_OS_USERS_OS_U_GOOGLESUBJECTID",
                table: "Users",
                newName: "IX_Users_GoogleSubjectId");

            migrationBuilder.RenameIndex(
                name: "IX_OS_USERS_OS_U_EMAIL",
                table: "Users",
                newName: "IX_Users_Email");

            migrationBuilder.RenameColumn(
                name: "OS_TR_TASKITEMID",
                table: "TaskDailyReminderDeliveries",
                newName: "TaskItemId");

            migrationBuilder.RenameColumn(
                name: "OS_TR_SENTATUTC",
                table: "TaskDailyReminderDeliveries",
                newName: "SentAtUtc");

            migrationBuilder.RenameColumn(
                name: "OS_TR_REMINDERDATE",
                table: "TaskDailyReminderDeliveries",
                newName: "ReminderDate");

            migrationBuilder.RenameColumn(
                name: "OS_TR_ID",
                table: "TaskDailyReminderDeliveries",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_OS_TASKS_REMINDERS_OS_TR_TASKITEMID_OS_TR_REMINDERDATE",
                table: "TaskDailyReminderDeliveries",
                newName: "IX_TaskDailyReminderDeliveries_TaskItemId_ReminderDate");

            migrationBuilder.RenameColumn(
                name: "OS_TO_TASKITEMID",
                table: "TaskOverdueNotificationDeliveries",
                newName: "TaskItemId");

            migrationBuilder.RenameColumn(
                name: "OS_TO_SENTATUTC",
                table: "TaskOverdueNotificationDeliveries",
                newName: "SentAtUtc");

            migrationBuilder.RenameColumn(
                name: "OS_TO_ID",
                table: "TaskOverdueNotificationDeliveries",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_OS_TASKS_OVERDUE_OS_TO_TASKITEMID",
                table: "TaskOverdueNotificationDeliveries",
                newName: "IX_TaskOverdueNotificationDeliveries_TaskItemId");

            migrationBuilder.RenameColumn(
                name: "OS_ST_USERID",
                table: "SyncTombstones",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "OS_ST_ENTITYTYPE",
                table: "SyncTombstones",
                newName: "EntityType");

            migrationBuilder.RenameColumn(
                name: "OS_ST_ENTITYID",
                table: "SyncTombstones",
                newName: "EntityId");

            migrationBuilder.RenameColumn(
                name: "OS_ST_DELETEDATUTC",
                table: "SyncTombstones",
                newName: "DeletedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OS_ST_ID",
                table: "SyncTombstones",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_OS_SYNC_TOMBSTONES_OS_ST_USERID_OS_ST_ENTITYTYPE_OS_ST_DELE~",
                table: "SyncTombstones",
                newName: "IX_SyncTombstones_UserId_EntityType_DeletedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OS_RT_USERID",
                table: "RefreshTokens",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "OS_RT_TOKENHASH",
                table: "RefreshTokens",
                newName: "TokenHash");

            migrationBuilder.RenameColumn(
                name: "OS_RT_REVOKEDATUTC",
                table: "RefreshTokens",
                newName: "RevokedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OS_RT_EXPIRESATUTC",
                table: "RefreshTokens",
                newName: "ExpiresAtUtc");

            migrationBuilder.RenameColumn(
                name: "OS_RT_CREATEDATUTC",
                table: "RefreshTokens",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OS_RT_ID",
                table: "RefreshTokens",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_OS_REFRESH_TOKENS_OS_RT_USERID",
                table: "RefreshTokens",
                newName: "IX_RefreshTokens_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_OS_REFRESH_TOKENS_OS_RT_TOKENHASH",
                table: "RefreshTokens",
                newName: "IX_RefreshTokens_TokenHash");

            migrationBuilder.RenameColumn(
                name: "OS_PS_USERID",
                table: "PushSubscriptions",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "OS_PS_TRANSPORT",
                table: "PushSubscriptions",
                newName: "Transport");

            migrationBuilder.RenameColumn(
                name: "OS_PS_P256DHBASE64",
                table: "PushSubscriptions",
                newName: "P256dhBase64");

            migrationBuilder.RenameColumn(
                name: "OS_PS_ENDPOINT",
                table: "PushSubscriptions",
                newName: "Endpoint");

            migrationBuilder.RenameColumn(
                name: "OS_PS_DEVICETOKEN",
                table: "PushSubscriptions",
                newName: "DeviceToken");

            migrationBuilder.RenameColumn(
                name: "OS_PS_DEVICEPLATFORM",
                table: "PushSubscriptions",
                newName: "DevicePlatform");

            migrationBuilder.RenameColumn(
                name: "OS_PS_CREATEDATUTC",
                table: "PushSubscriptions",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OS_PS_AUTHBASE64",
                table: "PushSubscriptions",
                newName: "AuthBase64");

            migrationBuilder.RenameColumn(
                name: "OS_PS_ID",
                table: "PushSubscriptions",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_OS_PUSH_SUBSCRIPTIONS_OS_PS_USERID",
                table: "PushSubscriptions",
                newName: "IX_PushSubscriptions_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_OS_PUSH_SUBSCRIPTIONS_OS_PS_ENDPOINT",
                table: "PushSubscriptions",
                newName: "IX_PushSubscriptions_Endpoint");

            migrationBuilder.RenameIndex(
                name: "IX_OS_PUSH_SUBSCRIPTIONS_OS_PS_DEVICETOKEN",
                table: "PushSubscriptions",
                newName: "IX_PushSubscriptions_DeviceToken");

            migrationBuilder.RenameColumn(
                name: "OS_PC_CREATEDATUTC",
                table: "PermissionCodes",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OS_PC_CODE",
                table: "PermissionCodes",
                newName: "Code");

            migrationBuilder.RenameColumn(
                name: "OS_PC_PERMISSION",
                table: "PermissionCodes",
                newName: "Permission");

            migrationBuilder.RenameColumn(
                name: "OS_NTFS_USERID",
                table: "NotificationSettings",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "OS_NTFS_SHOWEXCEPTIONDETAILS",
                table: "NotificationSettings",
                newName: "ShowExceptionDetails");

            migrationBuilder.RenameColumn(
                name: "OS_NTFS_RETENTIONDAYS",
                table: "NotificationSettings",
                newName: "RetentionDays");

            migrationBuilder.RenameColumn(
                name: "OS_NTFS_BANNERVISIBLESECONDS",
                table: "NotificationSettings",
                newName: "BannerVisibleSeconds");

            migrationBuilder.RenameColumn(
                name: "OS_NTFS_BANNERMINIMUMGAPSECONDS",
                table: "NotificationSettings",
                newName: "BannerMinimumGapSeconds");

            migrationBuilder.RenameColumn(
                name: "OS_NTFS_ALLOWSHARENOTIFICATIONS",
                table: "NotificationSettings",
                newName: "AllowShareNotifications");

            migrationBuilder.RenameColumn(
                name: "OS_NTFS_ALLOWPUSH",
                table: "NotificationSettings",
                newName: "AllowPush");

            migrationBuilder.RenameColumn(
                name: "OS_NTFS_ALLOWNOTIFICATIONS",
                table: "NotificationSettings",
                newName: "AllowNotifications");

            migrationBuilder.RenameColumn(
                name: "OS_NTFS_ALLOWMOBILEBANNER",
                table: "NotificationSettings",
                newName: "AllowMobileBanner");

            migrationBuilder.RenameColumn(
                name: "OS_NTFS_ALLOWEMAIL",
                table: "NotificationSettings",
                newName: "AllowEmail");

            migrationBuilder.RenameColumn(
                name: "OS_NTFS_ID",
                table: "NotificationSettings",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_OS_NOTIFICATIONS_SETTINGS_OS_NTFS_USERID",
                table: "NotificationSettings",
                newName: "IX_NotificationSettings_UserId");

            migrationBuilder.RenameColumn(
                name: "OS_IE_SENTATUTC",
                table: "InventoryExpiryNotificationDeliveries",
                newName: "SentAtUtc");

            migrationBuilder.RenameColumn(
                name: "OS_IE_INVENTORYITEMID",
                table: "InventoryExpiryNotificationDeliveries",
                newName: "InventoryItemId");

            migrationBuilder.RenameColumn(
                name: "OS_IE_EXPIRYDATE",
                table: "InventoryExpiryNotificationDeliveries",
                newName: "ExpiryDate");

            migrationBuilder.RenameColumn(
                name: "OS_IE_ID",
                table: "InventoryExpiryNotificationDeliveries",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_OS_INVENTORIES_EXPIRY_OS_IE_INVENTORYITEMID_OS_IE_EXPIRYDATE",
                table: "InventoryExpiryNotificationDeliveries",
                newName: "IX_InventoryExpiryNotificationDeliveries_InventoryItemId_Expir~");

            migrationBuilder.RenameColumn(
                name: "OS_ER_SENTATUTC",
                table: "EventReminderDeliveries",
                newName: "SentAtUtc");

            migrationBuilder.RenameColumn(
                name: "OS_ER_OCCURRENCESTARTUTC",
                table: "EventReminderDeliveries",
                newName: "OccurrenceStartUtc");

            migrationBuilder.RenameColumn(
                name: "OS_ER_MINUTESBEFORESTART",
                table: "EventReminderDeliveries",
                newName: "MinutesBeforeStart");

            migrationBuilder.RenameColumn(
                name: "OS_ER_CALENDAREVENTID",
                table: "EventReminderDeliveries",
                newName: "CalendarEventId");

            migrationBuilder.RenameColumn(
                name: "OS_ER_ID",
                table: "EventReminderDeliveries",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_OS_EVENTS_REMINDERS_OS_ER_CALENDAREVENTID_OS_ER_MINUTESBEFO~",
                table: "EventReminderDeliveries",
                newName: "IX_EventReminderDeliveries_CalendarEventId_MinutesBeforeStart_~");

            migrationBuilder.RenameColumn(
                name: "OS_D_USERID",
                table: "DiagnosticLogEntries",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "OS_D_TIMESTAMPUTC",
                table: "DiagnosticLogEntries",
                newName: "TimestampUtc");

            migrationBuilder.RenameColumn(
                name: "OS_D_RECEIVEDATUTC",
                table: "DiagnosticLogEntries",
                newName: "ReceivedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OS_D_PLATFORM",
                table: "DiagnosticLogEntries",
                newName: "Platform");

            migrationBuilder.RenameColumn(
                name: "OS_D_OPERATINGSYSTEMVERSION",
                table: "DiagnosticLogEntries",
                newName: "OperatingSystemVersion");

            migrationBuilder.RenameColumn(
                name: "OS_D_MESSAGE",
                table: "DiagnosticLogEntries",
                newName: "Message");

            migrationBuilder.RenameColumn(
                name: "OS_D_LEVEL",
                table: "DiagnosticLogEntries",
                newName: "Level");

            migrationBuilder.RenameColumn(
                name: "OS_D_DEVICEMODEL",
                table: "DiagnosticLogEntries",
                newName: "DeviceModel");

            migrationBuilder.RenameColumn(
                name: "OS_D_DETAIL",
                table: "DiagnosticLogEntries",
                newName: "Detail");

            migrationBuilder.RenameColumn(
                name: "OS_D_APPVERSION",
                table: "DiagnosticLogEntries",
                newName: "AppVersion");

            migrationBuilder.RenameColumn(
                name: "OS_D_ID",
                table: "DiagnosticLogEntries",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_OS_DIAGNOSTICS_OS_D_USERID_OS_D_RECEIVEDATUTC",
                table: "DiagnosticLogEntries",
                newName: "IX_DiagnosticLogEntries_UserId_ReceivedAtUtc");

            migrationBuilder.RenameIndex(
                name: "IX_OS_DIAGNOSTICS_OS_D_RECEIVEDATUTC",
                table: "DiagnosticLogEntries",
                newName: "IX_DiagnosticLogEntries_ReceivedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OP_TS_SOURCETASKLISTID",
                table: "TaskShares",
                newName: "SourceTaskListId");

            migrationBuilder.RenameColumn(
                name: "OP_TS_RECIPIENTUSERID",
                table: "TaskShares",
                newName: "RecipientUserId");

            migrationBuilder.RenameColumn(
                name: "OP_TS_OWNERUSERID",
                table: "TaskShares",
                newName: "OwnerUserId");

            migrationBuilder.RenameColumn(
                name: "OP_TS_CREATEDATUTC",
                table: "TaskShares",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OP_TS_ACCESSLEVEL",
                table: "TaskShares",
                newName: "AccessLevel");

            migrationBuilder.RenameColumn(
                name: "OP_TS_ACCEPTEDATUTC",
                table: "TaskShares",
                newName: "AcceptedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OP_TS_ID",
                table: "TaskShares",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_OP_TASKS_SHARED_OP_TS_SOURCETASKLISTID_OP_TS_RECIPIENTUSERID",
                table: "TaskShares",
                newName: "IX_TaskShares_SourceTaskListId_RecipientUserId");

            migrationBuilder.RenameColumn(
                name: "OP_TI_TASKID",
                table: "TaskItemEntity",
                newName: "TaskId");

            migrationBuilder.RenameColumn(
                name: "OP_TI_REMINDDAILY",
                table: "TaskItemEntity",
                newName: "RemindDaily");

            migrationBuilder.RenameColumn(
                name: "OP_TI_POSITION",
                table: "TaskItemEntity",
                newName: "Position");

            migrationBuilder.RenameColumn(
                name: "OP_TI_OVERDUENOTIFICATIONCHANNEL",
                table: "TaskItemEntity",
                newName: "OverdueNotificationChannel");

            migrationBuilder.RenameColumn(
                name: "OP_TI_LOCATION",
                table: "TaskItemEntity",
                newName: "Location");

            migrationBuilder.RenameColumn(
                name: "OP_TI_LINKEDINVENTORYITEMID",
                table: "TaskItemEntity",
                newName: "LinkedInventoryItemId");

            migrationBuilder.RenameColumn(
                name: "OP_TI_LINKEDCALENDAREVENTID",
                table: "TaskItemEntity",
                newName: "LinkedCalendarEventId");

            migrationBuilder.RenameColumn(
                name: "OP_TI_KIND",
                table: "TaskItemEntity",
                newName: "Kind");

            migrationBuilder.RenameColumn(
                name: "OP_TI_ISCOMPLETED",
                table: "TaskItemEntity",
                newName: "IsCompleted");

            migrationBuilder.RenameColumn(
                name: "OP_TI_DUEDATEUTC",
                table: "TaskItemEntity",
                newName: "DueDateUtc");

            migrationBuilder.RenameColumn(
                name: "OP_TI_DESCRIPTION",
                table: "TaskItemEntity",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "OP_TI_DAILYREMINDERTIMEOFDAYMINUTES",
                table: "TaskItemEntity",
                newName: "DailyReminderTimeOfDayMinutes");

            migrationBuilder.RenameColumn(
                name: "OP_TI_DAILYREMINDERNOTIFICATIONCHANNEL",
                table: "TaskItemEntity",
                newName: "DailyReminderNotificationChannel");

            migrationBuilder.RenameColumn(
                name: "OP_TI_ID",
                table: "TaskItemEntity",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_OP_TASKS_ITEMS_OP_TI_TASKID",
                table: "TaskItemEntity",
                newName: "IX_TaskItemEntity_TaskId");

            migrationBuilder.RenameColumn(
                name: "OP_TC_POSITION",
                table: "TaskItemCategoryEntity",
                newName: "Position");

            migrationBuilder.RenameColumn(
                name: "OP_TC_CATEGORY",
                table: "TaskItemCategoryEntity",
                newName: "Category");

            migrationBuilder.RenameColumn(
                name: "OP_TC_TASKITEMID",
                table: "TaskItemCategoryEntity",
                newName: "TaskItemId");

            migrationBuilder.RenameIndex(
                name: "IX_OP_TASKS_CATEGORIES_OP_TC_CATEGORY",
                table: "TaskItemCategoryEntity",
                newName: "IX_TaskItemCategoryEntity_Category");

            migrationBuilder.RenameColumn(
                name: "OP_T_USERID",
                table: "Tasks",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "OP_T_UPDATEDATUTC",
                table: "Tasks",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OP_T_TITLE",
                table: "Tasks",
                newName: "Title");

            migrationBuilder.RenameColumn(
                name: "OP_T_PRIORITY",
                table: "Tasks",
                newName: "Priority");

            migrationBuilder.RenameColumn(
                name: "OP_T_LOCKEXPIRESATUTC",
                table: "Tasks",
                newName: "LockExpiresAtUtc");

            migrationBuilder.RenameColumn(
                name: "OP_T_LOCKEDBYUSERNAME",
                table: "Tasks",
                newName: "LockedByUserName");

            migrationBuilder.RenameColumn(
                name: "OP_T_LOCKEDBYUSERID",
                table: "Tasks",
                newName: "LockedByUserId");

            migrationBuilder.RenameColumn(
                name: "OP_T_ISPRIVATE",
                table: "Tasks",
                newName: "IsPrivate");

            migrationBuilder.RenameColumn(
                name: "OP_T_ISPINNED",
                table: "Tasks",
                newName: "IsPinned");

            migrationBuilder.RenameColumn(
                name: "OP_T_ISGROUP",
                table: "Tasks",
                newName: "IsGroup");

            migrationBuilder.RenameColumn(
                name: "OP_T_ISCOMPLETED",
                table: "Tasks",
                newName: "IsCompleted");

            migrationBuilder.RenameColumn(
                name: "OP_T_ENCRYPTEDNONCE",
                table: "Tasks",
                newName: "EncryptedNonce");

            migrationBuilder.RenameColumn(
                name: "OP_T_ENCRYPTEDCIPHERTEXT",
                table: "Tasks",
                newName: "EncryptedCiphertext");

            migrationBuilder.RenameColumn(
                name: "OP_T_DESCRIPTION",
                table: "Tasks",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "OP_T_CREATEDATUTC",
                table: "Tasks",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OP_T_ID",
                table: "Tasks",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "OP_T_LINKEDINVENTORYID",
                table: "Tasks",
                newName: "LinkedWarehouseId");

            migrationBuilder.RenameIndex(
                name: "IX_OP_TASKS_OP_T_USERID",
                table: "Tasks",
                newName: "IX_Tasks_UserId");

            migrationBuilder.RenameColumn(
                name: "OP_NTF_USERID",
                table: "NotificationEntries",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "OP_NTF_URL",
                table: "NotificationEntries",
                newName: "Url");

            migrationBuilder.RenameColumn(
                name: "OP_NTF_TITLEARGUMENTS",
                table: "NotificationEntries",
                newName: "TitleArguments");

            migrationBuilder.RenameColumn(
                name: "OP_NTF_TITLE",
                table: "NotificationEntries",
                newName: "Title");

            migrationBuilder.RenameColumn(
                name: "OP_NTF_READATUTC",
                table: "NotificationEntries",
                newName: "ReadAtUtc");

            migrationBuilder.RenameColumn(
                name: "OP_NTF_KIND",
                table: "NotificationEntries",
                newName: "Kind");

            migrationBuilder.RenameColumn(
                name: "OP_NTF_DISMISSEDATUTC",
                table: "NotificationEntries",
                newName: "DismissedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OP_NTF_CREATEDATUTC",
                table: "NotificationEntries",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OP_NTF_BODYARGUMENTS",
                table: "NotificationEntries",
                newName: "BodyArguments");

            migrationBuilder.RenameColumn(
                name: "OP_NTF_BODY",
                table: "NotificationEntries",
                newName: "Body");

            migrationBuilder.RenameColumn(
                name: "OP_NTF_ID",
                table: "NotificationEntries",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_OP_NOTIFICATIONS_OP_NTF_USERID_OP_NTF_CREATEDATUTC",
                table: "NotificationEntries",
                newName: "IX_NotificationEntries_UserId_CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OP_NS_SOURCENOTEID",
                table: "NoteShares",
                newName: "SourceNoteId");

            migrationBuilder.RenameColumn(
                name: "OP_NS_RECIPIENTUSERID",
                table: "NoteShares",
                newName: "RecipientUserId");

            migrationBuilder.RenameColumn(
                name: "OP_NS_OWNERUSERID",
                table: "NoteShares",
                newName: "OwnerUserId");

            migrationBuilder.RenameColumn(
                name: "OP_NS_CREATEDATUTC",
                table: "NoteShares",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OP_NS_ACCESSLEVEL",
                table: "NoteShares",
                newName: "AccessLevel");

            migrationBuilder.RenameColumn(
                name: "OP_NS_ACCEPTEDATUTC",
                table: "NoteShares",
                newName: "AcceptedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OP_NS_ID",
                table: "NoteShares",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_OP_NOTES_SHARED_OP_NS_SOURCENOTEID_OP_NS_RECIPIENTUSERID",
                table: "NoteShares",
                newName: "IX_NoteShares_SourceNoteId_RecipientUserId");

            migrationBuilder.RenameColumn(
                name: "OP_N_USERID",
                table: "Notes",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "OP_N_UPDATEDATUTC",
                table: "Notes",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OP_N_TITLE",
                table: "Notes",
                newName: "Title");

            migrationBuilder.RenameColumn(
                name: "OP_N_PRIORITY",
                table: "Notes",
                newName: "Priority");

            migrationBuilder.RenameColumn(
                name: "OP_N_LOCKEXPIRESATUTC",
                table: "Notes",
                newName: "LockExpiresAtUtc");

            migrationBuilder.RenameColumn(
                name: "OP_N_LOCKEDBYUSERNAME",
                table: "Notes",
                newName: "LockedByUserName");

            migrationBuilder.RenameColumn(
                name: "OP_N_LOCKEDBYUSERID",
                table: "Notes",
                newName: "LockedByUserId");

            migrationBuilder.RenameColumn(
                name: "OP_N_ISPRIVATE",
                table: "Notes",
                newName: "IsPrivate");

            migrationBuilder.RenameColumn(
                name: "OP_N_ISPINNED",
                table: "Notes",
                newName: "IsPinned");

            migrationBuilder.RenameColumn(
                name: "OP_N_ENCRYPTEDNONCE",
                table: "Notes",
                newName: "EncryptedNonce");

            migrationBuilder.RenameColumn(
                name: "OP_N_ENCRYPTEDCIPHERTEXT",
                table: "Notes",
                newName: "EncryptedCiphertext");

            migrationBuilder.RenameColumn(
                name: "OP_N_CREATEDATUTC",
                table: "Notes",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OP_N_CONTENTJSON",
                table: "Notes",
                newName: "ContentJson");

            migrationBuilder.RenameColumn(
                name: "OP_N_ID",
                table: "Notes",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_OP_NOTES_OP_N_USERID",
                table: "Notes",
                newName: "IX_Notes_UserId");

            migrationBuilder.RenameColumn(
                name: "OP_L_UPDATEDATUTC",
                table: "SharedLocations",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OP_L_SHARERUSERID",
                table: "SharedLocations",
                newName: "SharerUserId");

            migrationBuilder.RenameColumn(
                name: "OP_L_RECIPIENTUSERID",
                table: "SharedLocations",
                newName: "RecipientUserId");

            migrationBuilder.RenameColumn(
                name: "OP_L_NONCEBASE64",
                table: "SharedLocations",
                newName: "NonceBase64");

            migrationBuilder.RenameColumn(
                name: "OP_L_ISCONTINUOUS",
                table: "SharedLocations",
                newName: "IsContinuous");

            migrationBuilder.RenameColumn(
                name: "OP_L_CIPHERTEXTBASE64",
                table: "SharedLocations",
                newName: "CiphertextBase64");

            migrationBuilder.RenameColumn(
                name: "OP_L_ID",
                table: "SharedLocations",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_OP_LOCATIONS_OP_L_SHARERUSERID_OP_L_RECIPIENTUSERID",
                table: "SharedLocations",
                newName: "IX_SharedLocations_SharerUserId_RecipientUserId");

            migrationBuilder.RenameIndex(
                name: "IX_OP_LOCATIONS_OP_L_RECIPIENTUSERID",
                table: "SharedLocations",
                newName: "IX_SharedLocations_RecipientUserId");

            migrationBuilder.RenameColumn(
                name: "OP_II_UPDATEDATUTC",
                table: "InventoryItems",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OP_II_UNIT",
                table: "InventoryItems",
                newName: "Unit");

            migrationBuilder.RenameColumn(
                name: "OP_II_QUANTITY",
                table: "InventoryItems",
                newName: "Quantity");

            migrationBuilder.RenameColumn(
                name: "OP_II_PRODUCTTYPE",
                table: "InventoryItems",
                newName: "ProductType");

            migrationBuilder.RenameColumn(
                name: "OP_II_POSITION",
                table: "InventoryItems",
                newName: "Position");

            migrationBuilder.RenameColumn(
                name: "OP_II_PENDINGRESTOCKTASKLISTID",
                table: "InventoryItems",
                newName: "PendingRestockTaskListId");

            migrationBuilder.RenameColumn(
                name: "OP_II_PENDINGRESTOCKTASKITEMID",
                table: "InventoryItems",
                newName: "PendingRestockTaskItemId");

            migrationBuilder.RenameColumn(
                name: "OP_II_NAME",
                table: "InventoryItems",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "OP_II_MINIMUMQUANTITY",
                table: "InventoryItems",
                newName: "MinimumQuantity");

            migrationBuilder.RenameColumn(
                name: "OP_II_ISCHECKEDREGULARLY",
                table: "InventoryItems",
                newName: "IsCheckedRegularly");

            migrationBuilder.RenameColumn(
                name: "OP_II_EXPIRYNOTIFICATIONCHANNEL",
                table: "InventoryItems",
                newName: "ExpiryNotificationChannel");

            migrationBuilder.RenameColumn(
                name: "OP_II_EXPIRYDATE",
                table: "InventoryItems",
                newName: "ExpiryDate");

            migrationBuilder.RenameColumn(
                name: "OP_II_CREATEDATUTC",
                table: "InventoryItems",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OP_II_CATEGORY",
                table: "InventoryItems",
                newName: "Category");

            migrationBuilder.RenameColumn(
                name: "OP_II_ID",
                table: "InventoryItems",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "OP_II_INVENTORYID",
                table: "InventoryItems",
                newName: "WarehouseId");

            migrationBuilder.RenameColumn(
                name: "OP_ES_SOURCECALENDAREVENTID",
                table: "CalendarEventShares",
                newName: "SourceCalendarEventId");

            migrationBuilder.RenameColumn(
                name: "OP_ES_RECIPIENTUSERID",
                table: "CalendarEventShares",
                newName: "RecipientUserId");

            migrationBuilder.RenameColumn(
                name: "OP_ES_OWNERUSERID",
                table: "CalendarEventShares",
                newName: "OwnerUserId");

            migrationBuilder.RenameColumn(
                name: "OP_ES_CREATEDATUTC",
                table: "CalendarEventShares",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OP_ES_ACCESSLEVEL",
                table: "CalendarEventShares",
                newName: "AccessLevel");

            migrationBuilder.RenameColumn(
                name: "OP_ES_ACCEPTEDATUTC",
                table: "CalendarEventShares",
                newName: "AcceptedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OP_ES_ID",
                table: "CalendarEventShares",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_OP_EVENTS_SHARED_OP_ES_SOURCECALENDAREVENTID_OP_ES_RECIPIEN~",
                table: "CalendarEventShares",
                newName: "IX_CalendarEventShares_SourceCalendarEventId_RecipientUserId");

            migrationBuilder.RenameColumn(
                name: "OP_E_USERID",
                table: "CalendarEvents",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "OP_E_UPDATEDATUTC",
                table: "CalendarEvents",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OP_E_TITLE",
                table: "CalendarEvents",
                newName: "Title");

            migrationBuilder.RenameColumn(
                name: "OP_E_STARTUTC",
                table: "CalendarEvents",
                newName: "StartUtc");

            migrationBuilder.RenameColumn(
                name: "OP_E_REMINDERSJSON",
                table: "CalendarEvents",
                newName: "RemindersJson");

            migrationBuilder.RenameColumn(
                name: "OP_E_REMINDERNOTIFICATIONCHANNEL",
                table: "CalendarEvents",
                newName: "ReminderNotificationChannel");

            migrationBuilder.RenameColumn(
                name: "OP_E_RECURRENCEUNTILUTC",
                table: "CalendarEvents",
                newName: "RecurrenceUntilUtc");

            migrationBuilder.RenameColumn(
                name: "OP_E_RECURRENCEOCCURRENCECOUNT",
                table: "CalendarEvents",
                newName: "RecurrenceOccurrenceCount");

            migrationBuilder.RenameColumn(
                name: "OP_E_RECURRENCEINTERVALCOUNT",
                table: "CalendarEvents",
                newName: "RecurrenceIntervalCount");

            migrationBuilder.RenameColumn(
                name: "OP_E_RECURRENCEFREQUENCY",
                table: "CalendarEvents",
                newName: "RecurrenceFrequency");

            migrationBuilder.RenameColumn(
                name: "OP_E_PRIORITY",
                table: "CalendarEvents",
                newName: "Priority");

            migrationBuilder.RenameColumn(
                name: "OP_E_NOTIFYATSTART",
                table: "CalendarEvents",
                newName: "NotifyAtStart");

            migrationBuilder.RenameColumn(
                name: "OP_E_LOCKEXPIRESATUTC",
                table: "CalendarEvents",
                newName: "LockExpiresAtUtc");

            migrationBuilder.RenameColumn(
                name: "OP_E_LOCKEDBYUSERNAME",
                table: "CalendarEvents",
                newName: "LockedByUserName");

            migrationBuilder.RenameColumn(
                name: "OP_E_LOCKEDBYUSERID",
                table: "CalendarEvents",
                newName: "LockedByUserId");

            migrationBuilder.RenameColumn(
                name: "OP_E_LOCATIONLONGITUDE",
                table: "CalendarEvents",
                newName: "LocationLongitude");

            migrationBuilder.RenameColumn(
                name: "OP_E_LOCATIONLATITUDE",
                table: "CalendarEvents",
                newName: "LocationLatitude");

            migrationBuilder.RenameColumn(
                name: "OP_E_LOCATIONADDRESS",
                table: "CalendarEvents",
                newName: "LocationAddress");

            migrationBuilder.RenameColumn(
                name: "OP_E_ISALLDAY",
                table: "CalendarEvents",
                newName: "IsAllDay");

            migrationBuilder.RenameColumn(
                name: "OP_E_GUESTSJSON",
                table: "CalendarEvents",
                newName: "GuestsJson");

            migrationBuilder.RenameColumn(
                name: "OP_E_ENDUTC",
                table: "CalendarEvents",
                newName: "EndUtc");

            migrationBuilder.RenameColumn(
                name: "OP_E_DESCRIPTION",
                table: "CalendarEvents",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "OP_E_CREATEDATUTC",
                table: "CalendarEvents",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OP_E_COLOR",
                table: "CalendarEvents",
                newName: "Color");

            migrationBuilder.RenameColumn(
                name: "OP_E_ID",
                table: "CalendarEvents",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_OP_EVENTS_OP_E_USERID",
                table: "CalendarEvents",
                newName: "IX_CalendarEvents_UserId");

            migrationBuilder.RenameColumn(
                name: "OP_CG_NAME",
                table: "ChatGroups",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "OP_CG_LASTMESSAGEATUTC",
                table: "ChatGroups",
                newName: "LastMessageAtUtc");

            migrationBuilder.RenameColumn(
                name: "OP_CG_CREATEDBYUSERID",
                table: "ChatGroups",
                newName: "CreatedByUserId");

            migrationBuilder.RenameColumn(
                name: "OP_CG_CREATEDATUTC",
                table: "ChatGroups",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OP_CG_ID",
                table: "ChatGroups",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "OP_CA_JOINEDUSERID",
                table: "ChatGroupAnnouncements",
                newName: "JoinedUserId");

            migrationBuilder.RenameColumn(
                name: "OP_CA_HISTORYSHARED",
                table: "ChatGroupAnnouncements",
                newName: "HistoryShared");

            migrationBuilder.RenameColumn(
                name: "OP_CA_GROUPID",
                table: "ChatGroupAnnouncements",
                newName: "GroupId");

            migrationBuilder.RenameColumn(
                name: "OP_CA_ANNOUNCEDATUTC",
                table: "ChatGroupAnnouncements",
                newName: "AnnouncedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OP_CA_ADDEDBYUSERID",
                table: "ChatGroupAnnouncements",
                newName: "AddedByUserId");

            migrationBuilder.RenameColumn(
                name: "OP_CA_ID",
                table: "ChatGroupAnnouncements",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_OP_CHATS_ANNOUNCEMENTS_OP_CA_GROUPID_OP_CA_JOINEDUSERID",
                table: "ChatGroupAnnouncements",
                newName: "IX_ChatGroupAnnouncements_GroupId_JoinedUserId");

            migrationBuilder.RenameColumn(
                name: "OP_C_SENTATUTC",
                table: "ChatMessages",
                newName: "SentAtUtc");

            migrationBuilder.RenameColumn(
                name: "OP_C_SENDERUSERID",
                table: "ChatMessages",
                newName: "SenderUserId");

            migrationBuilder.RenameColumn(
                name: "OP_C_RECIPIENTUSERID",
                table: "ChatMessages",
                newName: "RecipientUserId");

            migrationBuilder.RenameColumn(
                name: "OP_C_READATUTC",
                table: "ChatMessages",
                newName: "ReadAtUtc");

            migrationBuilder.RenameColumn(
                name: "OP_C_NONCEBASE64",
                table: "ChatMessages",
                newName: "NonceBase64");

            migrationBuilder.RenameColumn(
                name: "OP_C_ISSHAREDHISTORY",
                table: "ChatMessages",
                newName: "IsSharedHistory");

            migrationBuilder.RenameColumn(
                name: "OP_C_ISEDITED",
                table: "ChatMessages",
                newName: "IsEdited");

            migrationBuilder.RenameColumn(
                name: "OP_C_GROUPMESSAGEID",
                table: "ChatMessages",
                newName: "GroupMessageId");

            migrationBuilder.RenameColumn(
                name: "OP_C_GROUPID",
                table: "ChatMessages",
                newName: "GroupId");

            migrationBuilder.RenameColumn(
                name: "OP_C_EDITEDATUTC",
                table: "ChatMessages",
                newName: "EditedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OP_C_CIPHERTEXTBASE64",
                table: "ChatMessages",
                newName: "CiphertextBase64");

            migrationBuilder.RenameColumn(
                name: "OP_C_ID",
                table: "ChatMessages",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_OP_CHATS_OP_C_SENDERUSERID_OP_C_RECIPIENTUSERID",
                table: "ChatMessages",
                newName: "IX_ChatMessages_SenderUserId_RecipientUserId");

            migrationBuilder.RenameIndex(
                name: "IX_OP_CHATS_OP_C_RECIPIENTUSERID_OP_C_SENDERUSERID",
                table: "ChatMessages",
                newName: "IX_ChatMessages_RecipientUserId_SenderUserId");

            migrationBuilder.RenameIndex(
                name: "IX_OP_CHATS_OP_C_GROUPMESSAGEID",
                table: "ChatMessages",
                newName: "IX_ChatMessages_GroupMessageId");

            migrationBuilder.RenameIndex(
                name: "IX_OP_CHATS_OP_C_GROUPID",
                table: "ChatMessages",
                newName: "IX_ChatMessages_GroupId");

            migrationBuilder.RenameColumn(
                name: "OL_TI_POSITION",
                table: "TaskItemTaskListLinkEntity",
                newName: "Position");

            migrationBuilder.RenameColumn(
                name: "OL_TI_LINKEDTASKLISTID",
                table: "TaskItemTaskListLinkEntity",
                newName: "LinkedTaskListId");

            migrationBuilder.RenameColumn(
                name: "OL_TI_TASKITEMID",
                table: "TaskItemTaskListLinkEntity",
                newName: "TaskItemId");

            migrationBuilder.RenameIndex(
                name: "IX_OL_TASKS_ITEMS_OL_TI_LINKEDTASKLISTID",
                table: "TaskItemTaskListLinkEntity",
                newName: "IX_TaskItemTaskListLinkEntity_LinkedTaskListId");

            migrationBuilder.RenameColumn(
                name: "OL_PS_TOKEN",
                table: "PublicShareLinks",
                newName: "Token");

            migrationBuilder.RenameColumn(
                name: "OL_PS_REVOKEDATUTC",
                table: "PublicShareLinks",
                newName: "RevokedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OL_PS_OWNERUSERID",
                table: "PublicShareLinks",
                newName: "OwnerUserId");

            migrationBuilder.RenameColumn(
                name: "OL_PS_ITEMTYPE",
                table: "PublicShareLinks",
                newName: "ItemType");

            migrationBuilder.RenameColumn(
                name: "OL_PS_ITEMID",
                table: "PublicShareLinks",
                newName: "ItemId");

            migrationBuilder.RenameColumn(
                name: "OL_PS_CREATEDATUTC",
                table: "PublicShareLinks",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OL_PS_ID",
                table: "PublicShareLinks",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_OL_PUBLIC_SHARES_OL_PS_TOKEN",
                table: "PublicShareLinks",
                newName: "IX_PublicShareLinks_Token");

            migrationBuilder.RenameIndex(
                name: "IX_OL_PUBLIC_SHARES_OL_PS_OWNERUSERID_OL_PS_ITEMTYPE_OL_PS_ITE~",
                table: "PublicShareLinks",
                newName: "IX_PublicShareLinks_OwnerUserId_ItemType_ItemId");

            migrationBuilder.RenameColumn(
                name: "OL_IT_TASKLISTID",
                table: "InventoryManagedTaskLists",
                newName: "TaskListId");

            migrationBuilder.RenameColumn(
                name: "OL_IT_REFRESHTIMEOFDAYMINUTES",
                table: "InventoryManagedTaskLists",
                newName: "RefreshTimeOfDayMinutes");

            migrationBuilder.RenameColumn(
                name: "OL_IT_ONLYLINKEDWITHDUEDATE",
                table: "InventoryManagedTaskLists",
                newName: "OnlyLinkedWithDueDate");

            migrationBuilder.RenameColumn(
                name: "OL_IT_ID",
                table: "InventoryManagedTaskLists",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "OL_IT_INVENTORYID",
                table: "InventoryManagedTaskLists",
                newName: "WarehouseId");

            migrationBuilder.RenameIndex(
                name: "IX_OL_INVENTORIES_TASKS_OL_IT_INVENTORYID",
                table: "InventoryManagedTaskLists",
                newName: "IX_InventoryManagedTaskLists_WarehouseId");

            migrationBuilder.RenameColumn(
                name: "OL_C_OWNERUSERID",
                table: "Contacts",
                newName: "OwnerUserId");

            migrationBuilder.RenameColumn(
                name: "OL_C_LASTMESSAGEATUTC",
                table: "Contacts",
                newName: "LastMessageAtUtc");

            migrationBuilder.RenameColumn(
                name: "OL_C_ISARCHIVED",
                table: "Contacts",
                newName: "IsArchived");

            migrationBuilder.RenameColumn(
                name: "OL_C_HISTORYCLEAREDATUTC",
                table: "Contacts",
                newName: "HistoryClearedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OL_C_CREATEDATUTC",
                table: "Contacts",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OL_C_CONTACTUSERID",
                table: "Contacts",
                newName: "ContactUserId");

            migrationBuilder.RenameColumn(
                name: "OL_C_ID",
                table: "Contacts",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_OL_CONTACTS_OL_C_OWNERUSERID_OL_C_CONTACTUSERID",
                table: "Contacts",
                newName: "IX_Contacts_OwnerUserId_ContactUserId");

            migrationBuilder.RenameColumn(
                name: "OL_CM_USERID",
                table: "ChatGroupMembers",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "OL_CM_ROLE",
                table: "ChatGroupMembers",
                newName: "Role");

            migrationBuilder.RenameColumn(
                name: "OL_CM_JOINEDATUTC",
                table: "ChatGroupMembers",
                newName: "JoinedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OL_CM_ISARCHIVED",
                table: "ChatGroupMembers",
                newName: "IsArchived");

            migrationBuilder.RenameColumn(
                name: "OL_CM_GROUPID",
                table: "ChatGroupMembers",
                newName: "GroupId");

            migrationBuilder.RenameColumn(
                name: "OL_CM_ID",
                table: "ChatGroupMembers",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_OL_CHATS_MEMBERS_OL_CM_USERID",
                table: "ChatGroupMembers",
                newName: "IX_ChatGroupMembers_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_OL_CHATS_MEMBERS_OL_CM_GROUPID_OL_CM_USERID",
                table: "ChatGroupMembers",
                newName: "IX_ChatGroupMembers_GroupId_UserId");

            migrationBuilder.RenameColumn(
                name: "OL_CA_OTHERUSERID",
                table: "ChatConversationAccesses",
                newName: "OtherUserId");

            migrationBuilder.RenameColumn(
                name: "OL_CA_INITIATEDBYUSERID",
                table: "ChatConversationAccesses",
                newName: "InitiatedByUserId");

            migrationBuilder.RenameColumn(
                name: "OL_CA_CREATEDATUTC",
                table: "ChatConversationAccesses",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OL_CA_APPROVEDATUTC",
                table: "ChatConversationAccesses",
                newName: "ApprovedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OL_CA_ID",
                table: "ChatConversationAccesses",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_OL_CHATS_ACCESS_OL_CA_INITIATEDBYUSERID_OL_CA_OTHERUSERID",
                table: "ChatConversationAccesses",
                newName: "IX_ChatConversationAccesses_InitiatedByUserId_OtherUserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserVerificationCodes",
                table: "UserVerificationCodes",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserPermissions",
                table: "UserPermissions",
                columns: new[] { "UserId", "Permission" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Users",
                table: "Users",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TaskDailyReminderDeliveries",
                table: "TaskDailyReminderDeliveries",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TaskOverdueNotificationDeliveries",
                table: "TaskOverdueNotificationDeliveries",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SyncTombstones",
                table: "SyncTombstones",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RefreshTokens",
                table: "RefreshTokens",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PushSubscriptions",
                table: "PushSubscriptions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PermissionCodes",
                table: "PermissionCodes",
                column: "Permission");

            migrationBuilder.AddPrimaryKey(
                name: "PK_NotificationSettings",
                table: "NotificationSettings",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_InventoryExpiryNotificationDeliveries",
                table: "InventoryExpiryNotificationDeliveries",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_EventReminderDeliveries",
                table: "EventReminderDeliveries",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DiagnosticLogEntries",
                table: "DiagnosticLogEntries",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TaskShares",
                table: "TaskShares",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TaskItemEntity",
                table: "TaskItemEntity",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TaskItemCategoryEntity",
                table: "TaskItemCategoryEntity",
                columns: new[] { "TaskItemId", "Category" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Tasks",
                table: "Tasks",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_NotificationEntries",
                table: "NotificationEntries",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_NoteShares",
                table: "NoteShares",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Notes",
                table: "Notes",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SharedLocations",
                table: "SharedLocations",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_InventoryItems",
                table: "InventoryItems",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CalendarEventShares",
                table: "CalendarEventShares",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CalendarEvents",
                table: "CalendarEvents",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ChatGroups",
                table: "ChatGroups",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ChatGroupAnnouncements",
                table: "ChatGroupAnnouncements",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ChatMessages",
                table: "ChatMessages",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TaskItemTaskListLinkEntity",
                table: "TaskItemTaskListLinkEntity",
                columns: new[] { "TaskItemId", "LinkedTaskListId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_PublicShareLinks",
                table: "PublicShareLinks",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_InventoryManagedTaskLists",
                table: "InventoryManagedTaskLists",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Contacts",
                table: "Contacts",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ChatGroupMembers",
                table: "ChatGroupMembers",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ChatConversationAccesses",
                table: "ChatConversationAccesses",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatGroupAnnouncements_ChatGroups_GroupId",
                table: "ChatGroupAnnouncements",
                column: "GroupId",
                principalTable: "ChatGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatGroupMembers_ChatGroups_GroupId",
                table: "ChatGroupMembers",
                column: "GroupId",
                principalTable: "ChatGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskItemCategoryEntity_TaskItemEntity_TaskItemId",
                table: "TaskItemCategoryEntity",
                column: "TaskItemId",
                principalTable: "TaskItemEntity",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskItemEntity_Tasks_TaskId",
                table: "TaskItemEntity",
                column: "TaskId",
                principalTable: "Tasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskItemTaskListLinkEntity_TaskItemEntity_TaskItemId",
                table: "TaskItemTaskListLinkEntity",
                column: "TaskItemId",
                principalTable: "TaskItemEntity",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // The hand-written half of Up, in reverse - see the note there.
            migrationBuilder.DropPrimaryKey(
                name: "PK_OP_INVENTORIES",
                table: "OP_INVENTORIES");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OP_INVENTORIES_SHARED",
                table: "OP_INVENTORIES_SHARED");

            migrationBuilder.RenameColumn(
                name: "OP_I_ID",
                table: "OP_INVENTORIES",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "OP_I_CREATEDATUTC",
                table: "OP_INVENTORIES",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OP_I_DESCRIPTION",
                table: "OP_INVENTORIES",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "OP_I_ENCRYPTEDCIPHERTEXT",
                table: "OP_INVENTORIES",
                newName: "EncryptedCiphertext");

            migrationBuilder.RenameColumn(
                name: "OP_I_ENCRYPTEDNONCE",
                table: "OP_INVENTORIES",
                newName: "EncryptedNonce");

            migrationBuilder.RenameColumn(
                name: "OP_I_ISPRIVATE",
                table: "OP_INVENTORIES",
                newName: "IsPrivate");

            migrationBuilder.RenameColumn(
                name: "OP_I_LOCKEXPIRESATUTC",
                table: "OP_INVENTORIES",
                newName: "LockExpiresAtUtc");

            migrationBuilder.RenameColumn(
                name: "OP_I_LOCKEDBYUSERID",
                table: "OP_INVENTORIES",
                newName: "LockedByUserId");

            migrationBuilder.RenameColumn(
                name: "OP_I_LOCKEDBYUSERNAME",
                table: "OP_INVENTORIES",
                newName: "LockedByUserName");

            migrationBuilder.RenameColumn(
                name: "OP_I_NAME",
                table: "OP_INVENTORIES",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "OP_I_UPDATEDATUTC",
                table: "OP_INVENTORIES",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OP_I_USERID",
                table: "OP_INVENTORIES",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "OP_IS_ID",
                table: "OP_INVENTORIES_SHARED",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "OP_IS_ACCEPTEDATUTC",
                table: "OP_INVENTORIES_SHARED",
                newName: "AcceptedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OP_IS_ACCESSLEVEL",
                table: "OP_INVENTORIES_SHARED",
                newName: "AccessLevel");

            migrationBuilder.RenameColumn(
                name: "OP_IS_CREATEDATUTC",
                table: "OP_INVENTORIES_SHARED",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OP_IS_OWNERUSERID",
                table: "OP_INVENTORIES_SHARED",
                newName: "OwnerUserId");

            migrationBuilder.RenameColumn(
                name: "OP_IS_RECIPIENTUSERID",
                table: "OP_INVENTORIES_SHARED",
                newName: "RecipientUserId");

            migrationBuilder.RenameColumn(
                name: "OP_IS_SOURCEINVENTORYID",
                table: "OP_INVENTORIES_SHARED",
                newName: "SourceWarehouseId");

            migrationBuilder.RenameIndex(
                name: "ix_inventories_name_trgm",
                table: "OP_INVENTORIES",
                newName: "ix_warehouses_name_trgm");

            migrationBuilder.RenameIndex(
                name: "IX_OP_INVENTORIES_OP_I_USERID",
                table: "OP_INVENTORIES",
                newName: "IX_Warehouses_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_OP_INVENTORIES_SHARED_OP_IS_RECIPIENTUSERID",
                table: "OP_INVENTORIES_SHARED",
                newName: "IX_WarehouseShares_RecipientUserId");

            migrationBuilder.RenameIndex(
                name: "IX_OP_INVENTORIES_SHARED_OP_IS_SOURCEINVENTORYID_OP_IS_RECIPIE~",
                table: "OP_INVENTORIES_SHARED",
                newName: "IX_WarehouseShares_SourceWarehouseId_RecipientUserId");

            migrationBuilder.RenameTable(
                name: "OP_INVENTORIES",
                newName: "Warehouses");

            migrationBuilder.RenameTable(
                name: "OP_INVENTORIES_SHARED",
                newName: "WarehouseShares");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Warehouses",
                table: "Warehouses",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_WarehouseShares",
                table: "WarehouseShares",
                column: "Id");

        }
    }
}
