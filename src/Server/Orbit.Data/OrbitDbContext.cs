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
    public DbSet<EventReminderDeliveryEntity> EventReminderDeliveries => Set<EventReminderDeliveryEntity>();
    public DbSet<ContactEntity> Contacts => Set<ContactEntity>();
    public DbSet<ChatMessageEntity> ChatMessages => Set<ChatMessageEntity>();
    public DbSet<ChatConversationAccessEntity> ChatConversationAccesses => Set<ChatConversationAccessEntity>();
    public DbSet<CalendarEventShareEntity> CalendarEventShares => Set<CalendarEventShareEntity>();
    public DbSet<PushSubscriptionEntity> PushSubscriptions => Set<PushSubscriptionEntity>();
    public DbSet<TaskOverdueNotificationDeliveryEntity> TaskOverdueNotificationDeliveries => Set<TaskOverdueNotificationDeliveryEntity>();
    public DbSet<TaskDailyReminderDeliveryEntity> TaskDailyReminderDeliveries => Set<TaskDailyReminderDeliveryEntity>();
    public DbSet<InventoryItemEntity> InventoryItems => Set<InventoryItemEntity>();
    public DbSet<InventoryManagedTaskListEntity> InventoryManagedTaskLists => Set<InventoryManagedTaskListEntity>();
    public DbSet<InventoryExpiryNotificationDeliveryEntity> InventoryExpiryNotificationDeliveries => Set<InventoryExpiryNotificationDeliveryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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

        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Email).IsRequired().HasMaxLength(320);
            entity.Property(user => user.UserName).IsRequired().HasMaxLength(64);
            entity.Property(user => user.DisplayName).IsRequired().HasMaxLength(200);
            entity.Property(user => user.PasswordHash).IsRequired();
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
            // Every inventory query is scoped to a single user's items; this is the index that makes
            // those lookups fast instead of scanning the whole table.
            entity.HasIndex(item => item.UserId);
        });

        modelBuilder.Entity<InventoryManagedTaskListEntity>(entity =>
        {
            entity.HasKey(row => row.Id);
            // At most one managed task list per user - IInventoryManagedTaskListRepository relies on
            // this to decide insert-vs-update.
            entity.HasIndex(row => row.UserId).IsUnique();
        });

        modelBuilder.Entity<InventoryExpiryNotificationDeliveryEntity>(entity =>
        {
            entity.HasKey(delivery => delivery.Id);
            // A given (inventory item, expiry date) pair is only ever warned about once; this unique
            // index is what actually enforces that - see the entity's class comment.
            entity.HasIndex(delivery => new { delivery.InventoryItemId, delivery.ExpiryDate }).IsUnique();
        });
    }
}
