using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Orbit.Contracts.Calendar;
using Orbit.Contracts.Inventory;
using Orbit.Contracts.Notes;
using Orbit.Contracts.Tasks;

namespace Orbit.Mobile.Data;

/// <summary>
/// The phone's own database, brought up to date by EF migrations (see LocalDatabase in Orbit.Maui).
/// Migrations rather than EnsureCreated because an installed app already has a database: EnsureCreated
/// does nothing at all to one that exists, so a new table simply never appeared.
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

    public DbSet<LocalChatGroup> ChatGroups => Set<LocalChatGroup>();

    /// <summary>Whose data this database holds - see LocalStoreOwner and LocalStoreReset.</summary>
    public DbSet<LocalStoreOwner> StoreOwners => Set<LocalStoreOwner>();

    /// <summary>What this account may use - see LocalPermission.</summary>
    public DbSet<LocalPermission> Permissions => Set<LocalPermission>();

    /// <summary>The in-app notification feed, as this phone holds it - see LocalNotification.</summary>
    public DbSet<LocalNotification> Notifications => Set<LocalNotification>();

    /// <summary>Appointments made here that the server has not named yet - see PendingCalendarLink.</summary>
    public DbSet<PendingCalendarLink> PendingCalendarLinks => Set<PendingCalendarLink>();

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
            note.Property(entity => entity.CopyBaseLines)
                .HasConversion(LinesConverter)
                .Metadata.SetValueComparer(LinesComparer);
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
            taskList.Property(entity => entity.CopyBaseLines)
                .HasConversion(LinesConverter)
                .Metadata.SetValueComparer(LinesComparer);
        });

        // The server's id is the only id a notification has - nothing on a phone raises one.
        modelBuilder.Entity<LocalNotification>(notification => notification.HasKey(entity => entity.Id));

        // One event stands for one entry, so the event is the key - see PendingCalendarLink for why
        // it cannot be the entry: an entry made offline has no id until the server gives it one.
        modelBuilder.Entity<PendingCalendarLink>(link => link.HasKey(entity => entity.CalendarEventLocalId));

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
            calendarEvent.Property(entity => entity.CopyBaseLines)
                .HasConversion(LinesConverter)
                .Metadata.SetValueComparer(LinesComparer);
        });

        modelBuilder.Entity<LocalWarehouse>(warehouse =>
        {
            warehouse.HasKey(entity => entity.LocalId);
            warehouse.HasIndex(entity => entity.ServerId).IsUnique().HasFilter("\"ServerId\" IS NOT NULL");
            warehouse.Property(entity => entity.Items)
                .HasConversion(WarehouseItemsConverter)
                .Metadata.SetValueComparer(WarehouseItemsComparer);
            warehouse.Property(entity => entity.ItemArrivals)
                .HasConversion(ArrivalsConverter)
                .Metadata.SetValueComparer(ArrivalsComparer);
            warehouse.Property(entity => entity.CopyBaseLines)
                .HasConversion(LinesConverter)
                .Metadata.SetValueComparer(LinesComparer);
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
            message.HasIndex(entity => new { entity.GroupId, entity.SentAtUtc });
        });

        modelBuilder.Entity<OutgoingChatMessage>(message =>
        {
            message.HasKey(entity => entity.Id);
            message.HasIndex(entity => entity.Id);
        });

        modelBuilder.Entity<LocalContact>(contact => contact.HasKey(entity => entity.UserId));

        modelBuilder.Entity<LocalChatGroup>(group =>
        {
            group.HasKey(entity => entity.Id);
            group.Property(entity => entity.Members)
                .HasConversion(MembersConverter)
                .Metadata.SetValueComparer(MembersComparer);
        });

        // Keyed by the name itself: the server's answer is a set, and holding the same permission twice
        // means nothing.
        modelBuilder.Entity<LocalPermission>(permission => permission.HasKey(entity => entity.Name));
    }

    /// <summary>
    /// A note's lines are a list, and SQLite has no list column. Stored as JSON in one column rather
    /// than a child table because nothing ever queries an individual line - they are only read and
    /// written whole, with the note.
    /// </summary>
    private static readonly ValueConverter<IReadOnlyList<NoteContentLineDto>, string> ContentConverter = new(
        content => JsonSerializer.Serialize(content, LocalStoreSerializerContext.Default.IReadOnlyListNoteContentLineDto),
        stored => ReadList(stored, LocalStoreSerializerContext.Default.IReadOnlyListNoteContentLineDto));

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
        stored => ReadList(stored, LocalStoreSerializerContext.Default.IReadOnlyListTaskItemDto));

    /// <summary>Without this an edited item list is compared by reference and saved unchanged.</summary>
    private static readonly ValueComparer<IReadOnlyList<TaskItemDto>> ItemsComparer = new(
        (left, right) => left!.SequenceEqual(right!),
        items => items.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
        items => items.ToList());

    /// <summary>
    /// What a copy was taken from, rendered as lines and stored in one column. The same JSON-in-a-column
    /// answer every list here gets, and for the same reason: nothing ever queries one line of it.
    /// </summary>
    private static readonly ValueConverter<IReadOnlyList<string>, string> LinesConverter = new(
        lines => JsonSerializer.Serialize(lines, LocalStoreSerializerContext.Default.IReadOnlyListString),
        stored => ReadList(stored, LocalStoreSerializerContext.Default.IReadOnlyListString));

    /// <summary>Without this a changed snapshot is compared by reference and saved unchanged.</summary>
    private static readonly ValueComparer<IReadOnlyList<string>> LinesComparer = new(
        (left, right) => left!.SequenceEqual(right!),
        lines => lines.Aggregate(0, (hash, line) => HashCode.Combine(hash, line.GetHashCode())),
        lines => lines.ToList());

    /// <summary>A group's membership, in one column - nothing ever queries a single member.</summary>
    private static readonly ValueConverter<IReadOnlyList<LocalChatGroupMember>, string> MembersConverter = new(
        members => JsonSerializer.Serialize(members, LocalStoreSerializerContext.Default.IReadOnlyListLocalChatGroupMember),
        stored => ReadList(stored, LocalStoreSerializerContext.Default.IReadOnlyListLocalChatGroupMember));

    /// <summary>Without this a changed membership is compared by reference and saved unchanged.</summary>
    private static readonly ValueComparer<IReadOnlyList<LocalChatGroupMember>> MembersComparer = new(
        (left, right) => left!.SequenceEqual(right!),
        members => members.Aggregate(0, (hash, member) => HashCode.Combine(hash, member.GetHashCode())),
        members => members.ToList());

    /// <summary>What a warehouse holds, in one column - nothing ever queries a single item.</summary>
    private static readonly ValueConverter<IReadOnlyList<WarehouseItemDto>, string> WarehouseItemsConverter = new(
        items => JsonSerializer.Serialize(items, LocalStoreSerializerContext.Default.IReadOnlyListWarehouseItemDto),
        stored => ReadList(stored, LocalStoreSerializerContext.Default.IReadOnlyListWarehouseItemDto));

    /// <summary>When each batch arrived, in one column beside the items - see LocalWarehouse.ItemArrivals.</summary>
    private static readonly ValueConverter<IReadOnlyDictionary<Guid, DateTimeOffset>, string> ArrivalsConverter = new(
        arrivals => JsonSerializer.Serialize(
            arrivals, LocalStoreSerializerContext.Default.IReadOnlyDictionaryGuidDateTimeOffset),
        stored => ReadArrivals(stored));

    private static readonly ValueComparer<IReadOnlyDictionary<Guid, DateTimeOffset>> ArrivalsComparer = new(
        (left, right) => left!.Count == right!.Count && !left.Except(right).Any(),
        arrivals => arrivals.Aggregate(0, (hash, arrival) => HashCode.Combine(hash, arrival.GetHashCode())),
        arrivals => arrivals.ToDictionary(arrival => arrival.Key, arrival => arrival.Value));

    /// <inheritdoc cref="ReadList"/>
    private static IReadOnlyDictionary<Guid, DateTimeOffset> ReadArrivals(string stored)
        => stored.Length == 0
            ? new Dictionary<Guid, DateTimeOffset>()
            : JsonSerializer.Deserialize(
                stored, LocalStoreSerializerContext.Default.IReadOnlyDictionaryGuidDateTimeOffset)
                ?? new Dictionary<Guid, DateTimeOffset>();

    /// <summary>Without this an edited item list is compared by reference and saved unchanged.</summary>
    private static readonly ValueComparer<IReadOnlyList<WarehouseItemDto>> WarehouseItemsComparer = new(
        (left, right) => left!.SequenceEqual(right!),
        items => items.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
        items => items.ToList());

    /// <summary>
    /// Reads one of the JSON list columns above, treating a blank column as "none" rather than as
    /// broken JSON.
    ///
    /// That case is not hypothetical. A column added by a migration is backfilled with that migration's
    /// default, and the default EF picks for TEXT is an empty string - which threw on the first read of
    /// every row that existed before the migration ran, and took the screen reading them down with it.
    /// It happened once here already (see BlankListColumnTests); every list column now survives it.
    /// </summary>
    private static IReadOnlyList<TItem> ReadList<TItem>(string stored, JsonTypeInfo<IReadOnlyList<TItem>> typeInfo)
        => stored.Length == 0 ? [] : JsonSerializer.Deserialize(stored, typeInfo) ?? [];

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
