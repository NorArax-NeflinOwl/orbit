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
    private readonly SqliteConnection _connection;

    public LocalStore()
    {
        // The database lives as long as the connection does, so this one is held open deliberately.
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

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
        => new(new DbContextOptionsBuilder<OrbitLocalDbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();
}
