namespace Orbit.Core.Users;

/// <summary>
/// An Orbit account. Owns notes and, eventually, every other per-user resource in the app.
/// </summary>
public sealed class User
{
    public Guid Id { get; private set; }
    public string Email { get; private set; }
    public string UserName { get; private set; }
    public string DisplayName { get; private set; }
    public string PasswordHash { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private User(Guid id, string email, string userName, string displayName, string passwordHash, DateTimeOffset createdAtUtc)
    {
        Id = id;
        Email = email;
        UserName = userName;
        DisplayName = displayName;
        PasswordHash = passwordHash;
        CreatedAtUtc = createdAtUtc;
    }

    public static User Create(string email, string userName, string displayName, string passwordHash)
        => new(Guid.NewGuid(), email, userName, displayName, passwordHash, DateTimeOffset.UtcNow);

    /// <summary>
    /// Rebuilds a user from already-persisted values, bypassing creation rules.
    /// </summary>
    public static User FromPersistence(
        Guid id, string email, string userName, string displayName, string passwordHash, DateTimeOffset createdAtUtc)
        => new(id, email, userName, displayName, passwordHash, createdAtUtc);
}
