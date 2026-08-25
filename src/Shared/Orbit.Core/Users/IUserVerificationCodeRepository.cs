namespace Orbit.Core.Users;

public interface IUserVerificationCodeRepository
{
    Task AddAsync(UserVerificationCode code, CancellationToken cancellationToken);

    Task UpdateAsync(UserVerificationCode code, CancellationToken cancellationToken);

    /// <summary>
    /// The newest still-usable code of that purpose for the user, if any - callers verify the supplied
    /// code against its hash rather than looking a code up by value, so a database row never has to be
    /// searchable by the secret itself.
    /// </summary>
    Task<UserVerificationCode?> FindActiveAsync(Guid userId, VerificationCodePurpose purpose, CancellationToken cancellationToken);

    /// <summary>
    /// Invalidates every outstanding code of that purpose for the user - called before issuing a new one
    /// so that requesting a fresh code reliably retires the previous one instead of leaving several valid
    /// at once.
    /// </summary>
    Task ConsumeAllAsync(Guid userId, VerificationCodePurpose purpose, CancellationToken cancellationToken);
}
