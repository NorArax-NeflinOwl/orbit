using Orbit.Core;
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
    public DbSet<ChatGroupAnnouncementEntity> ChatGroupAnnouncements => Set<ChatGroupAnnouncementEntity>();
    public DbSet<CalendarEventShareEntity> CalendarEventShares => Set<CalendarEventShareEntity>();
    public DbSet<PushSubscriptionEntity> PushSubscriptions => Set<PushSubscriptionEntity>();
    public DbSet<TaskOverdueNotificationDeliveryEntity> TaskOverdueNotificationDeliveries => Set<TaskOverdueNotificationDeliveryEntity>();
    public DbSet<TaskDailyReminderDeliveryEntity> TaskDailyReminderDeliveries => Set<TaskDailyReminderDeliveryEntity>();
    public DbSet<InventoryEntity> Inventories => Set<InventoryEntity>();
    public DbSet<InventoryShareEntity> InventoryShares => Set<InventoryShareEntity>();
    public DbSet<InventoryItemEntity> InventoryItems => Set<InventoryItemEntity>();
    public DbSet<InventoryManagedTaskListEntity> InventoryManagedTaskLists => Set<InventoryManagedTaskListEntity>();
    public DbSet<InventoryExpiryNotificationDeliveryEntity> InventoryExpiryNotificationDeliveries => Set<InventoryExpiryNotificationDeliveryEntity>();
    public DbSet<NotificationSettingsEntity> NotificationSettings => Set<NotificationSettingsEntity>();
    public DbSet<NotificationEntryEntity> NotificationEntries => Set<NotificationEntryEntity>();
    public DbSet<DiagnosticLogEntryEntity> DiagnosticLogEntries => Set<DiagnosticLogEntryEntity>();
    public DbSet<SyncTombstoneEntity> SyncTombstones => Set<SyncTombstoneEntity>();
    public DbSet<PublicShareLinkEntity> PublicShareLinks => Set<PublicShareLinkEntity>();
    public DbSet<UserPermissionEntity> UserPermissions => Set<UserPermissionEntity>();
    public DbSet<PermissionCodeEntity> PermissionCodes => Set<PermissionCodeEntity>();
    public DbSet<RateLimitWindowEntity> RateLimitWindows => Set<RateLimitWindowEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Trigram similarity, which is what answers "you already have one of these" as somebody types a
        // name - see NameSuggestionRepository. Declared here so a fresh database gets the extension with
        // its first migration rather than needing a hand-run CREATE EXTENSION.
        modelBuilder.HasPostgresExtension("pg_trgm");

        modelBuilder.Entity<PermissionCodeEntity>(entity =>
        {
            // The permission is the key: one code per permission, so a second can never be minted
            // beside the one somebody was told.
            entity.HasKey(code => code.Permission);
            entity.Property(code => code.Permission).HasMaxLength(32);
            entity.Property(code => code.Code).IsRequired().HasMaxLength(32);
        });

        modelBuilder.Entity<RateLimitWindowEntity>(entity =>
        {
            // The pair is the identity, and it is what makes taking a permit a single statement: an
            // INSERT that conflicts on this key becomes the increment, so two replicas racing for the
            // same window cannot both read 4 and both write 5.
            entity.HasKey(window => new { window.Partition, window.WindowStart });

            // Long enough for the longest partition the policies build - a policy name and a Guid, or a
            // policy name and an IPv6 address - and bounded so the key stays indexable.
            entity.Property(window => window.Partition).IsRequired().HasMaxLength(128);
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
            entity.Property(note => note.Title).IsRequired().HasMaxLength(StoredTextLimits.Title);
            // Matches UserEntity.UserName's max length, since this is always copied from there.
            entity.Property(note => note.LockedByUserName).HasMaxLength(64);
            // Every note query is scoped to a single user's notes; this is the index that makes those
            // lookups fast instead of scanning the whole table.
            entity.HasIndex(note => note.UserId);
            // Matches ItemPriority.Normal, so rows written before this column existed read back as
            // the default rather than as an unparseable empty string.
            entity.Property(row => row.Priority).IsRequired().HasMaxLength(10)
                .HasDefaultValue(nameof(Orbit.Core.Abstractions.ItemPriority.Normal));
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
            entity.Property(task => task.Title).IsRequired().HasMaxLength(StoredTextLimits.Title);
            // Defaulted rather than nullable: every reader treats "no description" as an empty string,
            // and a column that can also be null would give them a second way to spell the same thing.
            entity.Property(task => task.Description).IsRequired()
                .HasMaxLength(StoredTextLimits.EventDescription).HasDefaultValue(string.Empty);
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
            entity.Property(item => item.Description).IsRequired().HasMaxLength(StoredTextLimits.TaskDescription);
            // The longer text about the entry. Defaulted so every row written before it existed reads as
            // "nobody wrote one" rather than null - see TaskItemEntity.Notes.
            entity.Property(item => item.Notes).IsRequired().HasMaxLength(StoredTextLimits.EventDescription)
                .HasDefaultValue(string.Empty);
            entity.Property(item => item.OverdueNotificationChannel).HasMaxLength(20);
            entity.Property(item => item.DailyReminderNotificationChannel).HasMaxLength(20);
            // Every entry written before kinds existed is the ordinary sort, and has nowhere to be.
            entity.Property(item => item.Kind).IsRequired().HasMaxLength(20)
                .HasDefaultValue(nameof(Orbit.Core.Tasks.TaskItemKind.Checklist));
            // Matches CalendarEventEntity.LocationAddress, since it holds the same sort of thing.
            entity.Property(item => item.Location).IsRequired().HasMaxLength(StoredTextLimits.Address).HasDefaultValue(string.Empty);

            // The lists this entry stands for. Owned by the entry and deleted with it, like the entries
            // themselves are owned by their list.
            entity.HasMany(item => item.LinkedTaskLists)
                .WithOne()
                .HasForeignKey(link => link.TaskItemId)
                .OnDelete(DeleteBehavior.Cascade);

            // What it is filed under, owned the same way.
            entity.HasMany(item => item.Categories)
                .WithOne()
                .HasForeignKey(category => category.TaskItemId)
                .OnDelete(DeleteBehavior.Cascade);

            // And what the product it describes is filed under, which is a different question - see
            // TaskItemProductCategoryEntity.
            entity.HasMany(item => item.ProductCategories)
                .WithOne()
                .HasForeignKey(category => category.TaskItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TaskItemCategoryEntity>(entity =>
        {
            // The category itself is half the key: an entry carries each one once, which is the same
            // rule TaskItem.Categories applies on the way in.
            entity.HasKey(category => new { category.TaskItemId, category.Category });
            entity.Property(category => category.Category).IsRequired().HasMaxLength(StoredTextLimits.Category);
            // Every page that offers a category filter first has to ask what categories there are.
            entity.HasIndex(category => category.Category);
        });

        modelBuilder.Entity<TaskItemProductCategoryEntity>(entity =>
        {
            // The category itself is half the key, the same as the entry's own: a product carries each
            // word once, which is the rule TaskItem applies on the way in.
            entity.HasKey(category => new { category.TaskItemId, category.Category });
            entity.Property(category => category.Category).IsRequired().HasMaxLength(StoredTextLimits.Category);
            // Asked of the whole account by the used-values list, the same as every other category here.
            entity.HasIndex(category => category.Category);
        });

        modelBuilder.Entity<InventoryItemCategoryEntity>(entity =>
        {
            // The category itself is half the key, the same as a task entry's: an item carries each one
            // once, which is the rule InventoryItem.Categories applies on the way in.
            entity.HasKey(category => new { category.InventoryItemId, category.Category });
            entity.Property(category => category.Category).IsRequired().HasMaxLength(StoredTextLimits.Category);
            // The inventory editor's own filter first has to ask what categories a shelf holds, and the
            // used-values list asks it of the whole account.
            entity.HasIndex(category => category.Category);
        });

        modelBuilder.Entity<TaskItemTaskListLinkEntity>(entity =>
        {
            entity.HasKey(link => new { link.TaskItemId, link.LinkedTaskListId });

            // No foreign key to the list being pointed at. A link to a list that has since been deleted
            // reads as "not completed" rather than as a failure (see LinkedTaskCompletionResolver), and
            // a constraint here would instead refuse the delete or silently take the entry with it.
            entity.HasIndex(link => link.LinkedTaskListId);
        });

        modelBuilder.Entity<CalendarEventEntity>(entity =>
        {
            entity.HasKey(calendarEvent => calendarEvent.Id);
            entity.Property(calendarEvent => calendarEvent.Title).IsRequired().HasMaxLength(StoredTextLimits.Title);
            entity.Property(calendarEvent => calendarEvent.Description).HasMaxLength(StoredTextLimits.EventDescription);
            entity.Property(calendarEvent => calendarEvent.LocationAddress).HasMaxLength(StoredTextLimits.Address);
            entity.Property(calendarEvent => calendarEvent.Color).HasMaxLength(StoredTextLimits.Color);
            entity.Property(calendarEvent => calendarEvent.RecurrenceFrequency).HasMaxLength(20);
            entity.Property(calendarEvent => calendarEvent.ReminderNotificationChannel).HasMaxLength(20);
            // Matches UserEntity.UserName's max length, since this is always copied from there.
            entity.Property(calendarEvent => calendarEvent.LockedByUserName).HasMaxLength(64);
            // Every calendar event query is scoped to a single user's events; this is the index that
            // makes those lookups fast instead of scanning the whole table.
            entity.HasIndex(calendarEvent => calendarEvent.UserId);
            // Matches ItemPriority.Normal, so rows written before this column existed read back as
            // the default rather than as an unparseable empty string.
            entity.Property(row => row.Priority).IsRequired().HasMaxLength(10)
                .HasDefaultValue(nameof(Orbit.Core.Abstractions.ItemPriority.Normal));
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
            entity.Property(user => user.UserName).IsRequired().HasMaxLength(StoredTextLimits.UserName);
            entity.Property(user => user.DisplayName).IsRequired().HasMaxLength(StoredTextLimits.DisplayName);
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

        // One GIN index per name the suggestions search. Without them a similarity query reads every row
        // the reader owns, which is fine at ten items and not at a thousand - and this runs on keystrokes.
        // Notes and CalendarEvents are only ever searched as part of TaskItemDescription's own query
        // (see NameSuggestionRepository.NamesFor) - they have no kind of their own - but a UNION ALL
        // branch benefits from its own index exactly as a standalone query would.
        modelBuilder.Entity<InventoryItemEntity>()
            .HasMany(item => item.Categories)
            .WithOne()
            .HasForeignKey(category => category.InventoryItemId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<InventoryItemEntity>()
            .HasIndex(item => item.Name)
            .HasDatabaseName("ix_inventory_items_name_trgm")
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");
        modelBuilder.Entity<InventoryEntity>()
            .HasIndex(inventory => inventory.Name)
            .HasDatabaseName("ix_inventories_name_trgm")
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");
        modelBuilder.Entity<TaskEntity>()
            .HasIndex(task => task.Title)
            .HasDatabaseName("ix_tasks_title_trgm")
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");
        modelBuilder.Entity<TaskItemEntity>()
            .HasIndex(item => item.Description)
            .HasDatabaseName("ix_task_items_description_trgm")
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");
        modelBuilder.Entity<NoteEntity>()
            .HasIndex(note => note.Title)
            .HasDatabaseName("ix_notes_title_trgm")
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");
        modelBuilder.Entity<CalendarEventEntity>()
            .HasIndex(calendarEvent => calendarEvent.Title)
            .HasDatabaseName("ix_calendar_events_title_trgm")
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");

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
            entity.Property(group => group.Name).IsRequired().HasMaxLength(StoredTextLimits.GroupName);

            // Members are only ever read and written through their group (see ChatGroupRepository), so
            // this navigation is all EF Core needs to know about them; the DbSet exists only so the
            // repository can delete removed rows explicitly.
            entity.HasMany(group => group.Members)
                .WithOne()
                .HasForeignKey(member => member.GroupId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InventoryManagedTaskListEntity>()
            .Property(row => row.RefreshTimeOfDayMinutes)
            // Nine in the morning, matching RestockListSettings.DefaultRefreshTimeOfDay - stated here as
            // well so the column's own default agrees with the domain's.
            .HasDefaultValue(9 * 60);
        // Both default to true in the schema, not only in the entity: every inventory that had a restock
        // list before these columns existed kept one and was reminded about it, and a migration that
        // read back "false" would silently switch that off for all of them.
        modelBuilder.Entity<InventoryManagedTaskListEntity>()
            .Property(row => row.IsEnabled)
            .HasDefaultValue(true);
        modelBuilder.Entity<InventoryManagedTaskListEntity>()
            .Property(row => row.RemindDaily)
            .HasDefaultValue(true);
        modelBuilder.Entity<InventoryManagedTaskListEntity>()
            .Property(row => row.ListPriority)
            .IsRequired().HasMaxLength(10)
            .HasDefaultValue(nameof(Orbit.Core.Abstractions.ItemPriority.Normal));
        // The same rule again for where the standing reminder is said: a row written before the column
        // existed is a list that was being reminded on the phone, and reading back an empty channel
        // would take that away.
        modelBuilder.Entity<InventoryManagedTaskListEntity>()
            .Property(row => row.ReminderNotificationChannel)
            .IsRequired().HasMaxLength(10)
            .HasDefaultValue(nameof(Orbit.Core.Notifications.NotificationChannel.Push));

        modelBuilder.Entity<ChatGroupAnnouncementEntity>(entity =>
        {
            entity.HasKey(announcement => announcement.Id);
            // Read as a group's whole set, and searched within a group for one person's latest arrival
            // (see IChatGroupAnnouncementRepository.FindLatestJoinAsync); one index covers both.
            entity.HasIndex(announcement => new { announcement.GroupId, announcement.JoinedUserId });
            // An announcement is part of its group's conversation and has no life without it, so a
            // deleted group takes its lines with it rather than leaving them behind unreachable.
            entity.HasOne<ChatGroupEntity>()
                .WithMany()
                .HasForeignKey(announcement => announcement.GroupId)
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
            entity.Property(subscription => subscription.Transport).IsRequired().HasMaxLength(20);
            entity.Property(subscription => subscription.DevicePlatform).HasMaxLength(20);
            // Both identify one destination, and re-registering the same one must update rather than
            // duplicate (see PushSubscriptionRepository.AddOrReplaceAsync). Unique with nulls allowed,
            // since a row only ever carries one of the two.
            entity.HasIndex(subscription => subscription.Endpoint).IsUnique();
            entity.HasIndex(subscription => subscription.DeviceToken).IsUnique();
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
            entity.Property(item => item.Name).IsRequired().HasMaxLength(StoredTextLimits.Title);
            entity.Property(item => item.ProductType).HasMaxLength(StoredTextLimits.ProductType);
            entity.Property(item => item.ExpiryNotificationChannel).HasMaxLength(20);
            // Every inventory query is scoped to a single inventory's items; this is the index that
            // makes those lookups fast instead of scanning the whole table.
            entity.HasIndex(item => item.InventoryId);
        });

        modelBuilder.Entity<InventoryEntity>(entity =>
        {
            entity.HasKey(inventory => inventory.Id);
            entity.Property(inventory => inventory.Name).IsRequired().HasMaxLength(StoredTextLimits.Title);
            entity.Property(inventory => inventory.Description).IsRequired()
                .HasMaxLength(StoredTextLimits.EventDescription).HasDefaultValue(string.Empty);
            // Matches UserEntity.UserName's max length, since this is always copied from there.
            entity.Property(inventory => inventory.LockedByUserName).HasMaxLength(64);
            // Listing a user's own inventories is the most common inventory query.
            entity.HasIndex(inventory => inventory.UserId);
        });

        modelBuilder.Entity<InventoryShareEntity>(entity =>
        {
            entity.HasKey(share => share.Id);
            entity.Property(share => share.AccessLevel).IsRequired().HasMaxLength(20);
            // Mirrors NoteShareEntity: one lookup per (inventory, recipient) pair for the duplicate
            // check and the accepted-grant read, plus a recipient-wide scan for "shared with me".
            entity.HasIndex(share => new { share.SourceInventoryId, share.RecipientUserId });
            entity.HasIndex(share => share.RecipientUserId);
        });

        modelBuilder.Entity<InventoryManagedTaskListEntity>(entity =>
        {
            entity.HasKey(row => row.Id);
            // At most one managed task list per inventory - IInventoryManagedTaskListRepository relies
            // on this to decide insert-vs-update.
            entity.HasIndex(row => row.InventoryId).IsUnique();
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
            // Matches ItemPriority.Normal, so rows written before this column existed read back as
            // the default a new list gets rather than as an unparseable empty string.
            entity.Property(row => row.Priority).IsRequired().HasMaxLength(10)
                .HasDefaultValue(nameof(Orbit.Core.Abstractions.ItemPriority.Normal));
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

        modelBuilder.Entity<SyncTombstoneEntity>(entity =>
        {
            entity.HasKey(tombstone => tombstone.Id);
            entity.Property(tombstone => tombstone.EntityType).IsRequired().HasMaxLength(40);
            // Every read is "this user's deletions of this kind since a moment" - see SyncTombstoneRepository.
            entity.HasIndex(tombstone => new { tombstone.UserId, tombstone.EntityType, tombstone.DeletedAtUtc });
        });

        modelBuilder.Entity<DiagnosticLogEntryEntity>(entity =>
        {
            entity.HasKey(entry => entry.Id);
            entity.Property(entry => entry.Level).IsRequired().HasMaxLength(20);
            entity.Property(entry => entry.Message).IsRequired().HasMaxLength(1000);
            entity.Property(entry => entry.Detail).HasMaxLength(4000);
            entity.Property(entry => entry.AppVersion).IsRequired().HasMaxLength(40);
            entity.Property(entry => entry.Platform).IsRequired().HasMaxLength(20);
            entity.Property(entry => entry.OperatingSystemVersion).IsRequired().HasMaxLength(40);
            entity.Property(entry => entry.DeviceModel).IsRequired().HasMaxLength(80);
            // Reads are "this user's most recent report"; retention sweeps by ReceivedAtUtc alone.
            entity.HasIndex(entry => new { entry.UserId, entry.ReceivedAtUtc });
            entity.HasIndex(entry => entry.ReceivedAtUtc);
        });

        modelBuilder.Entity<NotificationEntryEntity>(entity =>
        {
            entity.HasKey(entry => entry.Id);
            entity.Property(entry => entry.Kind).IsRequired().HasMaxLength(20);
            entity.Property(entry => entry.Title).IsRequired().HasMaxLength(200);
            entity.Property(entry => entry.Body).IsRequired().HasMaxLength(1000);
            // JSON, and nullable: an entry whose sentence has nothing in it stores nothing here.
            entity.Property(entry => entry.TitleArguments).HasMaxLength(1000);
            entity.Property(entry => entry.BodyArguments).HasMaxLength(2000);
            // The feed/unread-count queries are always scoped to one user, most-recent-first - this
            // index covers both without a separate sort step.
            entity.HasIndex(entry => new { entry.UserId, entry.CreatedAtUtc });
        });

        // Last, so it renames the finished model - declared properties and the shadow foreign keys
        // EF added along the way alike.
        OrbitStorageNames.ApplyTo(modelBuilder);
    }
}
