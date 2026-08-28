using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Orbit.Data;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// A real database in a temporary file, for the handful of tests whose subject is storage itself rather
/// than a handler - see TaskItemOrderTests and AccountDeletionSweepTests. SQLite rather than the
/// Postgres the app actually runs on: these are about what EF Core does with the rows, and a test that
/// needs a server running is a test that does not run.
///
/// It exists as a class rather than as three lines copied into each of those tests because of one
/// setting. <see cref="SqliteConnectionStringBuilder.Pooling"/> defaults to true, so disposing the
/// context hands its connection back to the pool instead of closing the file, and the pooled handle
/// outlives the test. Windows then refuses to delete the file and the run fails in teardown - with every
/// assertion having passed. POSIX unlinks an open file without complaint, so on macOS and Linux the same
/// mistake says nothing at all and waits for somebody to run the suite on Windows. With pooling off the
/// handle closes with the context and the file goes, identically on every platform.
/// </summary>
internal sealed class TemporarySqliteDatabase : IDisposable
{
    private readonly string _path;

    public OrbitDbContext DbContext { get; }

    public TemporarySqliteDatabase()
    {
        _path = Path.Combine(Path.GetTempPath(), $"orbit-tests-{Guid.NewGuid():N}.db");
        var connectionString = new SqliteConnectionStringBuilder { DataSource = _path, Pooling = false }.ToString();
        DbContext = new OrbitDbContext(
            new DbContextOptionsBuilder<OrbitDbContext>().UseSqlite(connectionString).Options);
        DbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        DbContext.Dispose();
        // Deliberately not swallowed: a failure here means a handle is still open, which is the very
        // thing this class is arranged to prevent.
        File.Delete(_path);
    }
}
