namespace Orbit.Data.Entities;

/// <summary>
/// Persistence shape of a user account, mapped separately from <see cref="Orbit.Core.Users.User"/> so
/// schema changes don't force changes onto domain logic, and vice versa.
/// </summary>
public sealed class UserEntity
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
}
