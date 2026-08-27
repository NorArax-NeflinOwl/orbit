using Microsoft.EntityFrameworkCore;
using Orbit.Data.Entities;

namespace Orbit.Data;

public sealed class OrbitDbContext : DbContext
{
    public OrbitDbContext(DbContextOptions<OrbitDbContext> options) : base(options)
    {
    }

    public DbSet<NoteEntity> Notes => Set<NoteEntity>();
    public DbSet<NoteShareEntity> NoteShares => Set<NoteShareEntity>();
    public DbSet<TaskEntity> Tasks => Set<TaskEntity>();
    public DbSet<TaskShareEntity> TaskShares => Set<TaskShareEntity>();
    public DbSet<CalendarEventEntity> CalendarEvents => Set<CalendarEventEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();
    public DbSet<UserVerificationCodeEntity> UserVerificationCodes => Set<UserVerificationCodeEntity>();
    public DbSet<EventReminderDeliveryEntity> EventReminderDeliveries => Set<EventReminderDeliveryEntity>();
    public DbSet<ContactEntity> Contacts => Set<ContactEntity>();
    public DbSet<ChatMessageEntity> ChatMessages => Set<ChatMessageEntity>();
    public DbSet<ChatConversationAccessEntity> ChatConversationAccesses => Set<ChatConversationAccessEntity>();
    public DbSet<SharedLocationEntity> SharedLocations => Set<SharedLocationEntity>();
    public DbSet<ChatGroupEntity> ChatGroups => Set<ChatGroupEntity>();
    public DbSet<ChatGroupMemberEntity> ChatGroupMembers => Set<ChatGroupMemberEntity>();
    public DbSet<CalendarEventShareEntity> CalendarEventShares => Set<CalendarEventShareEntity>();
    public DbSet<PushSubscriptionEntity> PushSubscriptions => Set<PushSubscriptionEntity>();
    public DbSet<TaskOverdueNotificationDeliveryEntity> TaskOverdueNotificationDeliveries => Set<TaskOverdueNotificationDeliveryEntity>();
    public DbSet<TaskDailyReminderDeliveryEntity> TaskDailyReminderDeliveries => Set<TaskDailyReminderDeliveryEntity>();
    public DbSet<WarehouseEntity> Warehouses => Set<WarehouseEntity>();
    public DbSet<WarehouseShareEntity> WarehouseShares => Set<WarehouseShareEntity>();
    public DbSet<InventoryItemEntity> InventoryItems => Set<InventoryItemEntity>();
    public DbSet<InventoryManagedTaskListEntity> InventoryManagedTaskLists => Set<InventoryManagedTaskListEntity>();
    public DbSet<InventoryExpiryNotificationDeliveryEntity> InventoryExpiryNotificationDeliveries => Set<InventoryExpiryNotificationDeliveryEntity>();
    public DbSet<NotificationSettingsEntity> NotificationSettings => Set<NotificationSettingsEntity>();
    public DbSet<NotificationEntryEntity> NotificationEntries => Set<NotificationEntryEntity>();
    public DbSet<PublicShareLinkEntity> PublicShareLinks => Set<PublicShareLinkEntity>();
    public DbSet<UserPermissionEntity> UserPermissions => Set<UserPermissionEntity>();
    public DbSet<PermissionCodeEntity> PermissionCodes => Set<PermissionCodeEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PermissionCodeEntity>(entity =>
        {
            // The permission is the key: one code per permission, so a second can never be minted
            // beside the one somebody was told.
            entity.HasKey(code => code.Permission);
            entity.Property(code => code.Permission).HasMaxLength(32);
            entity.Property(code => code.Code).IsRequired().HasMaxLength(32);
        });

        modelBuilder.Entity<UserPermissionEntity>(entity =>
        {
            // The pair is the identity: an account either holds a permission or it does not, and there
            // is no second way to hold the same one.
            entity.HasKey(permission => new { permission.UserId, permission.Permission });
            entity.Property(permission => permission.Permission).IsRequired().HasMaxLength(32);
        });

        modelBuilder.Entity<NoteEntity>(entity =>
        {
            entity.HasKey(note => note.Id);
            entity.Property(note => note.Title).IsRequired().HasMaxLength(200);
            // Matches UserEntity.UserName's max length, since this is always copied from there.
            entity.Property(note => note.LockedByUserName).HasMaxLength(64);
            // Every note query is scoped to a single user's notes; this is the index that makes those
            // lookups fast instead of scanning the whole table.
            entity.HasIndex(note => note.UserId);
        });

        modelBuilder.Entity<NoteShareEntity>(entity =>
        {
            entity.HasKey(share => share.Id);
            // ShareNoteCommandHandler's duplicate check (NoteShareRepository.FindExistingAsync) looks up
            // by this pair on every share attempt.
            entity.HasIndex(share => new { share.SourceNoteId, share.RecipientUserId });
        });

        modelBuilder.Entity<TaskEntity>(entity =>
        {
            entity.HasKey(task => task.Id);
            entity.Property(task => task.Title).IsRequired().HasMaxLength(200);
            // Matches UserEntity.UserName's max length, since this is always copied from there.
            entity.Property(task => task.LockedByUserName).HasMaxLength(64);
            // Every task list query is scoped to a single user's task lists; this is the index that
            // makes those lookups fast instead of scanning the whole table.
            entity.HasIndex(task => task.UserId);

            // Items are only ever read/written through their owning task list (see TaskRepository), so
            // there is no reason to expose a top-level DbSet<TaskItemEntity> - this navigation is the
            // only way EF Core needs to know about them.
            entity.HasMany(task => task.Items)
                .WithOne()
                .HasForeignKey(item => item.TaskId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TaskItemEntity>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Description).IsRequired().HasMaxLength(500);
            entity.Property(item => item.OverdueNotificationChannel).HasMaxLength(20);
            entity.Property(item => item.DailyReminderNotificationChannel).HasMaxLength(20);
        });

        modelBuilder.Entity<CalendarEventEntity>(entity =>
        {
            entity.HasKey(calendarEvent => calendarEvent.Id);
            entity.Property(calendarEvent => calendarEvent.Title).IsRequired().HasMaxLength(200);
            entity.Property(calendarEvent => calendarEvent.Description).HasMaxLength(2000);
            entity.Property(calendarEvent => calendarEvent.LocationAddress).HasMaxLength(300);
            entity.Property(calendarEvent => calendarEvent.Color).HasMaxLength(20);
            entity.Property(calendarEvent => calendarEvent.RecurrenceFrequency).HasMaxLength(20);
            entity.Property(calendarEvent => calendarEvent.CreationNotificationChannel).HasMaxLength(20);
            entity.Property(calendarEvent => calendarEvent.ReminderNotificationChannel).HasMaxLength(20);
            // Matches UserEntity.UserName's max length, since this is always copied from there.
            entity.Property(calendarEvent => calendarEvent.LockedByUserName).HasMaxLength(64);
            // Every calendar event query is scoped to a single user's events; this is the index that
            // makes those lookups fast instead of scanning the whole table.
            entity.HasIndex(calendarEvent => calendarEvent.UserId);
        });

        modelBuilder.Entity<CalendarEventShareEntity>(entity =>
        {
            entity.HasKey(share => share.Id);
            // ShareCalendarEventCommandHandler's duplicate check (CalendarEventShareRepository.FindExistingAsync)
            // looks up by this pair on every share attempt.
            entity.HasIndex(share => new { share.SourceCalendarEventId, share.RecipientUserId });
        });

        modelBuilder.Entity<TaskShareEntity>(entity =>
        {
            entity.HasKey(share => share.Id);
            // ShareTaskListCommandHandler's duplicate check (TaskListShareRepository.FindExistingAsync)
            // looks up by this pair on every share attempt.
            entity.HasIndex(share => new { share.SourceTaskListId, share.RecipientUserId });
        });

        modelBuilder.Entity<UserVerificationCodeEntity>(entity =>
        {
            entity.HasKey(code => code.Id);
            entity.Property(code => code.Purpose).IsRequired().HasMaxLength(30);
            entity.Property(code => code.CodeHash).IsRequired().HasMaxLength(200);
            entity.Property(code => code.EmailAddress).IsRequired().HasMaxLength(320);
            // Every lookup is "the newest usable code of this purpose for this user" - see
            // UserVerificationCodeRepository.FindActiveAsync.
            entity.HasIndex(code => new { code.UserId, code.Purpose });
        });

        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Email).IsRequired().HasMaxLength(320);
            entity.Property(user => user.UserName).IsRequired().HasMaxLength(64);
            entity.Property(user => user.DisplayName).IsRequired().HasMaxLength(200);
            // Nullable: a Google account has no password until it sets one.
            entity.Property(user => user.GoogleSubjectId).HasMaxLength(64);
            // Signing in with Google looks an account up by subject, so this is the index that makes it fast.
            entity.HasIndex(user => user.GoogleSubjectId);
            // A P-256 ECDH public key (raw, uncompressed) base64-encodes to about 88 characters; 200
            // leaves comfortable headroom without being unbounded.
            entity.Property(user => user.PublicKeyBase64).HasMaxLength(200);
            // The JWK-exported private key, AES-GCM-encrypted; JWK JSON for a P-256 key is small, but
            // base64 overhead and formatting leave room to spare.
            entity.Property(user => user.WrappedPrivateKeyBase64).HasMaxLength(1000);
            // A 12-byte AES-GCM nonce base64-encodes to exactly 16 characters.
            entity.Property(user => user.PrivateKeyWrapNonceBase64).HasMaxLength(16);
            // A 16-byte PBKDF2 salt base64-encodes to exactly 24 characters.
            entity.Property(user => user.PrivateKeySaltBase64).HasMaxLength(24);
            // Registration checks these before creating an account, and login looks users up by
            // either one; the unique indexes make all of that fast and rule out duplicate accounts or
            // duplicate usernames at the database level.
            entity.HasIndex(user => user.Email).IsUnique();
            entity.HasIndex(user => user.UserName).IsUnique();
        });

        modelBuilder.Entity<RefreshTokenEntity>(entity =>
        {
            entity.HasKey(refreshToken => refreshToken.Id);
            // SHA-256 hex digest is always exactly 64 characters.
            entity.Property(refreshToken => refreshToken.TokenHash).IsRequired().HasMaxLength(64);
            // A redeemed or revoked token is looked up by its hash on every refresh/logout call; this
            // unique index makes that lookup fast and guarantees hashes can't collide across rows.
            entity.HasIndex(refreshToken => refreshToken.TokenHash).IsUnique();
            entity.HasIndex(refreshToken => refreshToken.UserId);
        });

        modelBuilder.Entity<EventReminderDeliveryEntity>(entity =>
        {
            entity.HasKey(delivery => delivery.Id);
            // A given event/lead-time/occurrence triple is only ever sent once; this unique index is what
            // actually enforces that - EventReminderRepository's HasBeenSentAsync check is a check-then-act
            // read that alone can't guarantee it. OccurrenceStartUtc is part of the key (rather than just
            // CalendarEventId/MinutesBeforeStart) so a recurring event's reminders are tracked per
            // occurrence instead of only ever firing once for the whole series.
            entity.HasIndex(delivery => new { delivery.CalendarEventId, delivery.MinutesBeforeStart, delivery.OccurrenceStartUtc }).IsUnique();
        });

        modelBuilder.Entity<ContactEntity>(entity =>
        {
            entity.HasKey(contact => contact.Id);
            // A contact relationship in one direction is unique per owner/other-user pair -
            // ContactRepository.EnsureContactAsync relies on this to decide insert-vs-update - and this
            // is also the index that makes "list my contacts" fast.
            entity.HasIndex(contact => new { contact.OwnerUserId, contact.ContactUserId }).IsUnique();
        });

        modelBuilder.Entity<ChatMessageEntity>(entity =>
        {
            entity.HasKey(message => message.Id);
            entity.Property(message => message.CiphertextBase64).IsRequired();
            entity.Property(message => message.NonceBase64).IsRequired();
            // A conversation is fetched by either direction between two users (see
            // ChatMessageRepository.GetConversationAsync); these two indexes cover both.
            entity.HasIndex(message => new { message.SenderUserId, message.RecipientUserId });
            entity.HasIndex(message => new { message.RecipientUserId, message.SenderUserId });
            // A group conversation is fetched by group, then filtered to the copies one member can read
            // (see ChatMessageRepository.GetGroupConversationAsync), and deleting a posting looks every
            // copy up by the id they share.
            entity.HasIndex(message => message.GroupId);
            entity.HasIndex(message => message.GroupMessageId);
        });

        modelBuilder.Entity<SharedLocationEntity>(entity =>
        {
            entity.HasKey(shared => shared.Id);
            entity.Property(shared => shared.CiphertextBase64).IsRequired();
            entity.Property(shared => shared.NonceBase64).IsRequired();
            // One row per pair, enforced here rather than only in the handler - a refresh racing itself
            // would otherwise be able to leave two points behind, which is the history this must not keep.
            entity.HasIndex(shared => new { shared.SharerUserId, shared.RecipientUserId }).IsUnique();
            // "What is being shared with me" is the query the recipient polls.
            entity.HasIndex(shared => shared.RecipientUserId);
        });

        modelBuilder.Entity<ChatGroupEntity>(entity =>
        {
            entity.HasKey(group => group.Id);
            entity.Property(group => group.Name).IsRequired().HasMaxLength(120);

            // Members are only ever read and written through their group (see ChatGroupRepository), so
            // this navigation is all EF Core needs to know about them; the DbSet exists only so the
            // repository can delete removed rows explicitly.
            entity.HasMany(group => group.Members)
                .WithOne()
                .HasForeignKey(member => member.GroupId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatGroupMemberEntity>(entity =>
        {
            entity.HasKey(member => member.Id);
            entity.Property(member => member.Role).IsRequired().HasMaxLength(16);
            // "Which groups am I in" is the query behind the whole group list, and one person can only
            // be in a given group once.
            entity.HasIndex(member => member.UserId);
            entity.HasIndex(member => new { member.GroupId, member.UserId }).IsUnique();
        });

        modelBuilder.Entity<ChatConversationAccessEntity>(entity =>
        {
            entity.HasKey(access => access.Id);
            // Looked up by either user first (see ChatConversationAccessRepository.FindEntityAsync) -
            // this index speeds up the "InitiatedByUserId == me" half of that lookup; the reversed half
            // falls back to a table scan, acceptable at this app's scale.
            entity.HasIndex(access => new { access.InitiatedByUserId, access.OtherUserId }).IsUnique();
        });

        modelBuilder.Entity<PushSubscriptionEntity>(entity =>
        {
            entity.HasKey(subscription => subscription.Id);
            entity.Property(subscription => subscription.Endpoint).IsRequired();
            entity.Property(subscription => subscription.P256dhBase64).IsRequired();
            entity.Property(subscription => subscription.AuthBase64).IsRequired();
            // A browser's subscription endpoint is unique across all users - this is what
            // PushSubscriptionRepository.AddOrReplaceAsync relies on to decide insert-vs-update, and
            // also the index that makes "who does this endpoint belong to" fast.
            entity.HasIndex(subscription => subscription.Endpoint).IsUnique();
            // Every "notify this user" fan-out (see PushNotificationDispatcher) looks up subscriptions
            // by UserId; this index makes that fast instead of scanning the whole table.
            entity.HasIndex(subscription => subscription.UserId);
        });

        modelBuilder.Entity<TaskOverdueNotificationDeliveryEntity>(entity =>
        {
            entity.HasKey(delivery => delivery.Id);
            // A given task item is only ever notified about once; this unique index is what actually
            // enforces that - OverdueTaskNotificationRepository.HasBeenNotifiedAsync's check is a
            // check-then-act read that alone can't guarantee it (see EventReminderDeliveryEntity for the
            // same reasoning applied to calendar event reminders).
            entity.HasIndex(delivery => delivery.TaskItemId).IsUnique();
        });

        modelBuilder.Entity<TaskDailyReminderDeliveryEntity>(entity =>
        {
            entity.HasKey(delivery => delivery.Id);
            // A given (task item, local date) pair is only ever reminded about once; this unique index
            // is what actually enforces that - DailyTaskReminderRepository.HasBeenSentAsync's check is a
            // check-then-act read that alone can't guarantee it (see TaskOverdueNotificationDeliveryEntity
            // above for the same reasoning applied to overdue-task notifications).
            entity.HasIndex(delivery => new { delivery.TaskItemId, delivery.ReminderDate }).IsUnique();
        });

        modelBuilder.Entity<InventoryItemEntity>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).IsRequired().HasMaxLength(200);
            entity.Property(item => item.ProductType).HasMaxLength(100);
            entity.Property(item => item.Category).HasMaxLength(100);
            entity.Property(item => item.ExpiryNotificationChannel).HasMaxLength(20);
            // Every inventory query is scoped to a single warehouse's items; this is the index that
            // makes those lookups fast instead of scanning the whole table.
            entity.HasIndex(item => item.WarehouseId);
        });

        modelBuilder.Entity<WarehouseEntity>(entity =>
        {
            entity.HasKey(warehouse => warehouse.Id);
            entity.Property(warehouse => warehouse.Name).IsRequired().HasMaxLength(200);
            // Matches UserEntity.UserName's max length, since this is always copied from there.
            entity.Property(warehouse => warehouse.LockedByUserName).HasMaxLength(64);
            // Listing a user's own warehouses is the most common warehouse query.
            entity.HasIndex(warehouse => warehouse.UserId);
        });

        modelBuilder.Entity<WarehouseShareEntity>(entity =>
        {
            entity.HasKey(share => share.Id);
            entity.Property(share => share.AccessLevel).IsRequired().HasMaxLength(20);
            // Mirrors NoteShareEntity: one lookup per (warehouse, recipient) pair for the duplicate
            // check and the accepted-grant read, plus a recipient-wide scan for "shared with me".
            entity.HasIndex(share => new { share.SourceWarehouseId, share.RecipientUserId });
            entity.HasIndex(share => share.RecipientUserId);
        });

        modelBuilder.Entity<InventoryManagedTaskListEntity>(entity =>
        {
            entity.HasKey(row => row.Id);
            // At most one managed task list per warehouse - IInventoryManagedTaskListRepository relies
            // on this to decide insert-vs-update.
            entity.HasIndex(row => row.WarehouseId).IsUnique();
        });

        modelBuilder.Entity<InventoryExpiryNotificationDeliveryEntity>(entity =>
        {
            entity.HasKey(delivery => delivery.Id);
            // A given (inventory item, expiry date) pair is only ever warned about once; this unique
            // index is what actually enforces that - see the entity's class comment.
            entity.HasIndex(delivery => new { delivery.InventoryItemId, delivery.ExpiryDate }).IsUnique();
        });

        modelBuilder.Entity<NotificationSettingsEntity>(entity =>
        {
            entity.HasKey(row => row.Id);
            // At most one settings row per user - NotificationSettingsRepository relies on this to
            // decide insert-vs-update.
            entity.HasIndex(row => row.UserId).IsUnique();
            // Matches BannerTiming.Default, so rows written before these columns existed read back as
            // the same defaults a brand-new settings row gets rather than a 0-second banner.
            entity.Property(row => row.BannerVisibleSeconds).HasDefaultValue(5);
            entity.Property(row => row.BannerMinimumGapSeconds).HasDefaultValue(5);
            // Same reason: an account that predates this column should get the window a new account
            // gets, not the 0 an int column would otherwise default to - which NotificationSettings
            // would then clamp up to 1, quietly shortening it.
            entity.Property(row => row.RetentionDays).HasDefaultValue(Orbit.Core.Notifications.NotificationSettings.DefaultRetentionDays);
        });

        modelBuilder.Entity<TaskEntity>(entity =>
        {
            // Matches TaskListPriority.Normal, so rows written before this column existed read back as
            // the default a new list gets rather than as an unparseable empty string.
            entity.Property(row => row.Priority).IsRequired().HasMaxLength(10)
                .HasDefaultValue(nameof(Orbit.Core.Tasks.TaskListPriority.Normal));
        });

        modelBuilder.Entity<PublicShareLinkEntity>(entity =>
        {
            entity.HasKey(link => link.Id);
            entity.Property(link => link.Token).IsRequired().HasMaxLength(64);
            entity.Property(link => link.ItemType).IsRequired().HasMaxLength(20);
            // The token is the entire access check, so every read behind a link is a lookup by it -
            // unique both to make that an index seek and because two links can't share a secret.
            entity.HasIndex(link => link.Token).IsUnique();
            // GetLiveForItemAsync's exact filter: one live link per item per owner.
            entity.HasIndex(link => new { link.OwnerUserId, link.ItemType, link.ItemId });
        });

        modelBuilder.Entity<NotificationEntryEntity>(entity =>
        {
            entity.HasKey(entry => entry.Id);
            entity.Property(entry => entry.Kind).IsRequired().HasMaxLength(20);
            entity.Property(entry => entry.Title).IsRequired().HasMaxLength(200);
            entity.Property(entry => entry.Body).IsRequired().HasMaxLength(1000);
            // The feed/unread-count queries are always scoped to one user, most-recent-first - this
            // index covers both without a separate sort step.
            entity.HasIndex(entry => new { entry.UserId, entry.CreatedAtUtc });
        });
    }
}
