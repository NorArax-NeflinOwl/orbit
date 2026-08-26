using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.ConfirmEmailVerification;

public sealed class ConfirmEmailVerificationCommandHandler
    : IRequestHandler<ConfirmEmailVerificationCommand, EmailVerificationConfirmResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IUserVerificationCodeRepository _verificationCodeRepository;
    private readonly IVerificationCodeGenerator _codeGenerator;

    public ConfirmEmailVerificationCommandHandler(
        IUserRepository userRepository, IUserVerificationCodeRepository verificationCodeRepository,
        IVerificationCodeGenerator codeGenerator)
    {
        _userRepository = userRepository;
        _verificationCodeRepository = verificationCodeRepository;
        _codeGenerator = codeGenerator;
    }

    public async Task<EmailVerificationConfirmResult> HandleAsync(
        ConfirmEmailVerificationCommand request, CancellationToken cancellationToken)
    {
        var storedCode = await _verificationCodeRepository.FindActiveAsync(
            request.UserId, VerificationCodePurpose.EmailVerification, cancellationToken);
        if (storedCode is null)
        {
            return EmailVerificationConfirmResult.InvalidCode;
        }

        if (!_codeGenerator.Verify(request.Code, storedCode.CodeHash))
        {
            // Counted, so a six-digit code can't be ground down by repeated guessing - see UserVerificationCode.
            storedCode.RecordFailedAttempt();
            await _verificationCodeRepository.UpdateAsync(storedCode, cancellationToken);
            return EmailVerificationConfirmResult.InvalidCode;
        }

        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return EmailVerificationConfirmResult.InvalidCode;
        }

        // Asked again here, not just when the code was issued: the address only has to be free at the
        // moment it is actually taken. Without this the write reaches the unique index on Users.Email
        // and the request dies as a 500 - see EmailVerificationConfirmResult.EmailTaken.
        var existing = await _userRepository.GetByEmailAsync(storedCode.EmailAddress, cancellationToken);
        if (existing is not null && existing.Id != user.Id)
        {
            // The code is left alive on purpose: nothing about it was wrong, and the reader may well be
            // able to use it once the collision is theirs to resolve.
            return EmailVerificationConfirmResult.EmailTaken;
        }

        storedCode.Consume();
        await _verificationCodeRepository.UpdateAsync(storedCode, cancellationToken);

        user.SetVerifiedEmail(storedCode.EmailAddress);
        await _userRepository.UpdateAsync(user, cancellationToken);
        return EmailVerificationConfirmResult.Confirmed;
    }
}
