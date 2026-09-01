using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Orbit.Mobile.Data;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// A real SQLite database held in memory, rather than EF's in-memory provider. The provider does not
/// enforce keys, unique indexes, or column types, so it would happily accept a schema the phone cannot
/// actually store - and the local store's job is to be a database.
/// </summary>
internal sealed class LocalStore : IDbContextFactory<OrbitLocalDbContext>, IDisposable
{
    /// <summary>
    /// Named, and shared between connections, so that every context can open one of its own onto the
    /// same database. A plain ":memory:" database belongs to a single connection, which forced every
    /// context here to share one - and sharing it is what produced "unable to delete/modify
    /// user-function due to active statements": EF registers its SQL functions while initialising each
    /// connection, and SQLite refuses that while any statement on it is still open. Two overlapping
    /// contexts were enough, so the failure arrived only under load, on CI, in a test about something
    /// else entirely.
    ///
    /// A connection each is also what the app really does - the phone's store is a file - so this makes
    /// the double behave like the thing it stands for, which is the whole reason these tests use real
    /// SQLite rather than EF's in-memory provider.
    /// </summary>
    private readonly string _connectionString =
        $"Data Source=file:{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    /// <summary>
    /// Held open for as long as this store lives, and used for nothing else. A shared-cache database
    /// exists only while some connection to it is open; without this it would be discarded between one
    /// context closing and the next opening, taking the schema and every row with it.
    /// </summary>
    private readonly SqliteConnection _keepAlive;

    public LocalStore()
    {
        _keepAlive = new SqliteConnection(_connectionString);
        _keepAlive.Open();

        // Migrate, not EnsureCreated, so a migration that does not actually produce the schema the code
        // expects fails here rather than on a device.
        using var schema = CreateDbContext();
        schema.Database.Migrate();
    }

    /// <summary>
    /// A fresh context onto the same database, which is exactly what the app does - so a test asserting
    /// through this sees what was really written rather than what one context happens to still track.
    /// </summary>
    public OrbitLocalDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<OrbitLocalDbContext>().UseSqlite(_connectionString).Options);

    public void Dispose() => _keepAlive.Dispose();
}
