using Microsoft.EntityFrameworkCore;
using Orbit.Api.Auth;
using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Users;
using Orbit.Core.Users.DeleteAccount;
using Orbit.Data;
using Orbit.Data.Entities;
using Orbit.Data.Repositories;
using Xunit;

namespace Orbit.Api.Tests.Users;

/// <summary>
/// Deleting an account that signed up with Google and never set a password, through the real handler, the
/// real user and deletion repositories, and a real schema - the combination the handler tests replace with
/// doubles, and the one a report of "a Google account cannot delete itself" has to be checked against
/// before blaming anything else. It passes: an empty password is what such an account sends, and nothing
/// in the schema (no foreign key reaches OS_USERS) stands in the way of the final delete.
///
/// The chat groups are the one in-memory double left: ChatGroupRepository orders by a DateTimeOffset,
/// which SQLite cannot translate and PostgreSQL can, so the real one fails here for a reason that has
/// nothing to do with deletion.
/// </summary>
public sealed class PasswordlessAccountDeletionTests : IDisposable
{
    private readonly TemporarySqliteDatabase _database = new();
    private readonly OrbitDbContext _dbContext;

    public PasswordlessAccountDeletionTests() => _dbContext = _database.DbContext;

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task A_google_account_without_a_password_is_deleted_with_an_empty_password()
    {
        var users = new UserRepository(_dbContext);
        var googleUser = User.CreateFromGoogle("gina@example.com", "gina", "Gina", "google-subject-gina");
        await users.AddAsync(googleUser, CancellationToken.None);

        var now = DateTimeOffset.UtcNow;
        _dbContext.Notes.Add(new NoteEntity { Id = Guid.NewGuid(), UserId = googleUser.Id, Title = "Note", ContentJson = "[]", CreatedAtUtc = now, UpdatedAtUtc = now });
        await _dbContext.SaveChangesAsync();

        var handler = new DeleteAccountCommandHandler(
            users, new PasswordHasher(), new AccountDeletionRepository(_dbContext), new InMemoryChatGroupRepository(),
            new StubGoogleIdentityVerifier(subjectId: "google-subject-gina"));

        var deleted = await handler.HandleAsync(new DeleteAccountCommand(googleUser.Id, string.Empty), CancellationToken.None);

        Assert.True(deleted);
        Assert.Null(await users.GetByIdAsync(googleUser.Id, CancellationToken.None));
        Assert.Empty(await _dbContext.Notes.Where(note => note.UserId == googleUser.Id).ToListAsync());
    }
}
