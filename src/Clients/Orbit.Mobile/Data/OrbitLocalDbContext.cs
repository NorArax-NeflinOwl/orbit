using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Orbit.Contracts.Calendar;
using Orbit.Contracts.Inventory;
using Orbit.Contracts.Notes;
using Orbit.Contracts.Tasks;

namespace Orbit.Mobile.Data;

/// <summary>
/// The phone's own database. Created with EnsureCreated rather than migrations for now: nothing has
/// shipped, so there is no installed schema to migrate from - see the note on
/// <see cref="OpenAsync"/> before that stops being true.
/// </summary>
public sealed class OrbitLocalDbContext : DbContext
{
    public OrbitLocalDbContext(DbContextOptions<OrbitLocalDbContext> options) : base(options)
    {
    }

    public DbSet<LocalNote> Notes => Set<LocalNote>();

    public DbSet<LocalTaskList> TaskLists => Set<LocalTaskList>();

    public DbSet<LocalCalendarEvent> CalendarEvents => Set<LocalCalendarEvent>();

    public DbSet<LocalWarehouse> Warehouses => Set<LocalWarehouse>();

    public DbSet<OutboxEntry> Outbox => Set<OutboxEntry>();

    public DbSet<SyncCursor> SyncCursors => Set<SyncCursor>();

    public DbSet<LocalChatMessage> ChatMessages => Set<LocalChatMessage>();

    public DbSet<OutgoingChatMessage> OutgoingChatMessages => Set<OutgoingChatMessage>();

    public DbSet<LocalContact> Contacts => Set<LocalContact>();

    /// <summary>
    /// SQLite has no date type, and EF's default mapping for <see cref="DateTimeOffset"/> cannot be
    /// sorted or compared in SQL - "ORDER BY UpdatedAtUtc" fails outright. Since sync is decided almost
    /// entirely by comparing timestamps, storing them as UTC ticks is not a detail: it is what lets the
    /// database answer "most recently changed first" and "changed since" at all.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        => configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcTicksConverter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LocalNote>(note =>
        {
            note.HasKey(entity => entity.LocalId);
            // Two rows must never claim the same server note. Filtered, because every note created
            // offline has no server id yet and they would otherwise all collide with each other.
            note.HasIndex(entity => entity.ServerId).IsUnique().HasFilter("\"ServerId\" IS NOT NULL");
            note.Property(entity => entity.Content)
                .HasConversion(ContentConverter)
                .Metadata.SetValueComparer(ContentComparer);
        });

        modelBuilder.Entity<LocalTaskList>(taskList =>
        {
            taskList.HasKey(entity => entity.LocalId);
            // Same filtered-unique rule as notes: every list created offline has no server id yet, and
            // they would otherwise all collide with each other.
            taskList.HasIndex(entity => entity.ServerId).IsUnique().HasFilter("\"ServerId\" IS NOT NULL");
            taskList.Property(entity => entity.Items)
                .HasConversion(ItemsConverter)
                .Metadata.SetValueComparer(ItemsComparer);
        });

        modelBuilder.Entity<LocalCalendarEvent>(calendarEvent =>
        {
            calendarEvent.HasKey(entity => entity.LocalId);
            calendarEvent.HasIndex(entity => entity.ServerId).IsUnique().HasFilter("\"ServerId\" IS NOT NULL");
            // Everything the event is travels as one block, so it is stored as one - nothing ever
            // queries a guest or a reminder on its own.
            calendarEvent.Property(entity => entity.Details)
                .HasConversion(
                    details => JsonSerializer.Serialize(details, LocalStoreSerializerContext.Default.CalendarEventDetailsDto),
                    stored => JsonSerializer.Deserialize(stored, LocalStoreSerializerContext.Default.CalendarEventDetailsDto)!);
        });

        modelBuilder.Entity<LocalWarehouse>(warehouse =>
        {
            warehouse.HasKey(entity => entity.LocalId);
            warehouse.HasIndex(entity => entity.ServerId).IsUnique().HasFilter("\"ServerId\" IS NOT NULL");
            warehouse.Property(entity => entity.Items)
                .HasConversion(WarehouseItemsConverter)
                .Metadata.SetValueComparer(WarehouseItemsComparer);
        });

        modelBuilder.Entity<OutboxEntry>(entry =>
        {
            entry.HasKey(entity => entity.Id);
            // Replay reads one entity type's changes in queue order, which is the only order that
            // reconstructs what happened.
            entry.HasIndex(entity => new { entity.EntityType, entity.Id });
        });

        modelBuilder.Entity<SyncCursor>(cursor => cursor.HasKey(entity => entity.EntityType));

        modelBuilder.Entity<LocalChatMessage>(message =>
        {
            message.HasKey(entity => entity.Id);
            // Every read is "this conversation, in order", which is the only way chat is ever queried.
            message.HasIndex(entity => new { entity.OtherUserId, entity.SentAtUtc });
        });

        modelBuilder.Entity<OutgoingChatMessage>(message =>
        {
            message.HasKey(entity => entity.Id);
            message.HasIndex(entity => entity.Id);
        });

        modelBuilder.Entity<LocalContact>(contact => contact.HasKey(entity => entity.UserId));
    }

    /// <summary>
    /// A note's lines are a list, and SQLite has no list column. Stored as JSON in one column rather
    /// than a child table because nothing ever queries an individual line - they are only read and
    /// written whole, with the note.
    /// </summary>
    private static readonly ValueConverter<IReadOnlyList<NoteContentLineDto>, string> ContentConverter = new(
        content => JsonSerializer.Serialize(content, LocalStoreSerializerContext.Default.IReadOnlyListNoteContentLineDto),
        stored => JsonSerializer.Deserialize(stored, LocalStoreSerializerContext.Default.IReadOnlyListNoteContentLineDto) ?? new List<NoteContentLineDto>());

    /// <summary>
    /// Without this EF compares the converted strings by reference and never notices an edit, so a note
    /// whose lines changed would be saved unchanged.
    /// </summary>
    private static readonly ValueComparer<IReadOnlyList<NoteContentLineDto>> ContentComparer = new(
        (left, right) => left!.SequenceEqual(right!),
        content => content.Aggregate(0, (hash, line) => HashCode.Combine(hash, line.GetHashCode())),
        content => content.ToList());

    /// <summary>
    /// A task list's items are a list, and SQLite has no list column - the same problem a note's lines
    /// have, and the same answer: JSON in one column, because nothing ever queries an individual item.
    /// </summary>
    private static readonly ValueConverter<IReadOnlyList<TaskItemDto>, string> ItemsConverter = new(
        items => JsonSerializer.Serialize(items, LocalStoreSerializerContext.Default.IReadOnlyListTaskItemDto),
        stored => JsonSerializer.Deserialize(stored, LocalStoreSerializerContext.Default.IReadOnlyListTaskItemDto) ?? new List<TaskItemDto>());

    /// <summary>Without this an edited item list is compared by reference and saved unchanged.</summary>
    private static readonly ValueComparer<IReadOnlyList<TaskItemDto>> ItemsComparer = new(
        (left, right) => left!.SequenceEqual(right!),
        items => items.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
        items => items.ToList());

    /// <summary>What a warehouse holds, in one column - nothing ever queries a single item.</summary>
    private static readonly ValueConverter<IReadOnlyList<WarehouseItemDto>, string> WarehouseItemsConverter = new(
        items => JsonSerializer.Serialize(items, LocalStoreSerializerContext.Default.IReadOnlyListWarehouseItemDto),
        stored => JsonSerializer.Deserialize(stored, LocalStoreSerializerContext.Default.IReadOnlyListWarehouseItemDto) ?? new List<WarehouseItemDto>());

    /// <summary>Without this an edited item list is compared by reference and saved unchanged.</summary>
    private static readonly ValueComparer<IReadOnlyList<WarehouseItemDto>> WarehouseItemsComparer = new(
        (left, right) => left!.SequenceEqual(right!),
        items => items.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
        items => items.ToList());

    /// <summary>
    /// Everything stored here is UTC - the server sends UTC and the app stamps UTC - so the offset
    /// carries no information and is dropped rather than round-tripped. Ticks sort correctly as integers,
    /// which is the whole point of the conversion.
    /// </summary>
    private sealed class UtcTicksConverter : ValueConverter<DateTimeOffset, long>
    {
        public UtcTicksConverter()
            : base(value => value.UtcDateTime.Ticks, stored => new DateTimeOffset(stored, TimeSpan.Zero))
        {
        }
    }
}
