using Microsoft.EntityFrameworkCore;
using Orbit.Core.Users;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly OrbitDbContext _dbContext;

    public UserRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == id, cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Email == email, cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task<User?> GetByUserNameAsync(string userName, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.UserName == userName, cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        _dbContext.Users.Add(ToEntity(user));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken)
    {
        _dbContext.Users.Update(ToEntity(user));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<User?> GetByGoogleSubjectIdAsync(string googleSubjectId, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.GoogleSubjectId == googleSubjectId, cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    private static User ToDomain(UserEntity entity)
        => User.FromPersistence(
            entity.Id, entity.Email, entity.UserName, entity.DisplayName, entity.PasswordHash, entity.CreatedAtUtc,
            entity.PublicKeyBase64, ToWrappedPrivateKey(entity), entity.EmailVerifiedAtUtc, entity.GoogleSubjectId,
            ToLocation(entity), ToPresence(entity));

    private static UserEntity ToEntity(User user)
        => new()
        {
            Id = user.Id,
            Email = user.Email,
            UserName = user.UserName,
            DisplayName = user.DisplayName,
            PasswordHash = user.PasswordHash,
            GoogleSubjectId = user.GoogleSubjectId,
            CreatedAtUtc = user.CreatedAtUtc,
            EmailVerifiedAtUtc = user.EmailVerifiedAtUtc,
            PublicKeyBase64 = user.PublicKeyBase64,
            WrappedPrivateKeyBase64 = user.WrappedPrivateKey?.CiphertextBase64,
            PrivateKeyWrapNonceBase64 = user.WrappedPrivateKey?.NonceBase64,
            PrivateKeySaltBase64 = user.WrappedPrivateKey?.SaltBase64,
            PrivateKeyDerivationIterations = user.WrappedPrivateKey?.Iterations,
            LocationLatitude = user.Location?.Latitude,
            LocationLongitude = user.Location?.Longitude,
            LocationAddress = user.Location?.Address,
            LocationRecordedAtUtc = user.Location?.RecordedAtUtc,
            PresenceAvailability = user.Presence.Availability.ToString(),
            PresenceLastSeenAtUtc = user.Presence.LastSeenAtUtc
        };

    /// <summary>
    /// An unreadable stored availability falls back to Available rather than throwing: what somebody
    /// chose to be is not worth making their whole row unreadable over, and the last-seen timestamp
    /// alongside it still decides whether they show as here at all.
    /// </summary>
    private static UserPresence ToPresence(UserEntity entity)
        => new(
            Enum.TryParse<PresenceAvailability>(entity.PresenceAvailability, out var availability)
                ? availability
                : PresenceAvailability.Available,
            entity.PresenceLastSeenAtUtc);

    /// <summary>
    /// Read back the same way the wrapped-key columns are: a location exists only when the coordinates
    /// and the timestamp are all there, rather than trusting one column to decide.
    /// </summary>
    private static UserLocation? ToLocation(UserEntity entity)
        => entity.LocationLatitude is { } latitude && entity.LocationLongitude is { } longitude
            && entity.LocationRecordedAtUtc is { } recordedAtUtc
            ? new UserLocation(entity.LocationAddress, latitude, longitude, recordedAtUtc)
            : null;

    /// <summary>
    /// The four wrapped-private-key columns are only ever written together (see ToEntity) and read back
    /// together here - null unless every one of them is present, rather than trusting just one to decide
    /// whether a backup exists.
    /// </summary>
    private static WrappedPrivateKey? ToWrappedPrivateKey(UserEntity entity)
    {
        if (entity.WrappedPrivateKeyBase64 is null || entity.PrivateKeyWrapNonceBase64 is null ||
            entity.PrivateKeySaltBase64 is null || entity.PrivateKeyDerivationIterations is null)
        {
            return null;
        }

        return new WrappedPrivateKey(
            entity.WrappedPrivateKeyBase64, entity.PrivateKeyWrapNonceBase64, entity.PrivateKeySaltBase64,
            entity.PrivateKeyDerivationIterations.Value);
    }
    public async Task<IReadOnlyList<User>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var entities = await _dbContext.Users
            .AsNoTracking()
            .Where(user => ids.Contains(user.Id))
            .ToListAsync(cancellationToken);

        return entities.Select(ToDomain).ToList();
    }
}