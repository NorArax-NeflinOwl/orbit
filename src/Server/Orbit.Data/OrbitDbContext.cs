using Microsoft.EntityFrameworkCore;
using Orbit.Data.Entities;

namespace Orbit.Data;

public sealed class OrbitDbContext : DbContext
{
    public OrbitDbContext(DbContextOptions<OrbitDbContext> options) : base(options)
    {
    }

    public DbSet<NoteEntity> Notes => Set<NoteEntity>();
    public DbSet<TaskEntity> Tasks => Set<TaskEntity>();
    public DbSet<CalendarEventEntity> CalendarEvents => Set<CalendarEventEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();
    public DbSet<EventReminderDeliveryEntity> EventReminderDeliveries => Set<EventReminderDeliveryEntity>();
    public DbSet<ContactEntity> Contacts => Set<ContactEntity>();
    public DbSet<ChatMessageEntity> ChatMessages => Set<ChatMessageEntity>();
    public DbSet<CalendarEventShareEntity> CalendarEventShares => Set<CalendarEventShareEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NoteEntity>(entity =>
        {
            entity.HasKey(note => note.Id);
            entity.Property(note => note.Title).IsRequired().HasMaxLength(200);
            entity.Property(note => note.Content).IsRequired();
            // Every note query is scoped to a single user's notes; this is the index that makes those
            // lookups fast instead of scanning the whole table.
            entity.HasIndex(note => note.UserId);
        });

        modelBuilder.Entity<TaskEntity>(entity =>
        {
            entity.HasKey(task => task.Id);
            entity.Property(task => task.Title).IsRequired().HasMaxLength(200);
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
        });

        modelBuilder.Entity<CalendarEventEntity>(entity =>
        {
            entity.HasKey(calendarEvent => calendarEvent.Id);
            entity.Property(calendarEvent => calendarEvent.Title).IsRequired().HasMaxLength(200);
            entity.Property(calendarEvent => calendarEvent.Description).HasMaxLength(2000);
            entity.Property(calendarEvent => calendarEvent.LocationAddress).HasMaxLength(300);
            entity.Property(calendarEvent => calendarEvent.Color).HasMaxLength(20);
            entity.Property(calendarEvent => calendarEvent.RecurrenceFrequency).HasMaxLength(20);
            // Matches UserEntity.UserName's max length, since this is always copied from there.
            entity.Property(calendarEvent => calendarEvent.SharedByUserName).HasMaxLength(64);
            // Every calendar event query is scoped to a single user's events; this is the index that
            // makes those lookups fast instead of scanning the whole table.
            entity.HasIndex(calendarEvent => calendarEvent.UserId);
        });

        modelBuilder.Entity<CalendarEventShareEntity>(entity =>
        {
            entity.HasKey(share => share.Id);
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
            // A given event/lead-time pair is only ever sent once; this unique index is what actually
            // enforces that - EventReminderRepository's HasBeenSentAsync check is a check-then-act read
            // that alone can't guarantee it.
            entity.HasIndex(delivery => new { delivery.CalendarEventId, delivery.MinutesBeforeStart }).IsUnique();
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
    }
}
