using Orbit.Core.Users;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>In-memory <see cref="IUserVerificationCodeRepository"/> stub for unit tests.</summary>
internal sealed class InMemoryUserVerificationCodeRepository : IUserVerificationCodeRepository
{
    private readonly List<UserVerificationCode> _codes = [];

    public Task AddAsync(UserVerificationCode code, CancellationToken cancellationToken)
    {
        _codes.Add(code);
        return Task.CompletedTask;
    }

    /// <summary>The domain object is already the stored instance here, so mutations need no write-back - mirrors InMemoryNoteRepository.UpdateAsync.</summary>
    public Task UpdateAsync(UserVerificationCode code, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<UserVerificationCode?> FindActiveAsync(
        Guid userId, VerificationCodePurpose purpose, CancellationToken cancellationToken)
        => Task.FromResult(
            _codes.Where(code => code.UserId == userId && code.Purpose == purpose && code.IsActive)
                .OrderByDescending(code => code.CreatedAtUtc)
                .FirstOrDefault());

    public Task ConsumeAllAsync(Guid userId, VerificationCodePurpose purpose, CancellationToken cancellationToken)
    {
        foreach (var code in _codes.Where(code => code.UserId == userId && code.Purpose == purpose))
        {
            code.Consume();
        }

        return Task.CompletedTask;
    }
}
