using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.ConfirmEmailVerification;

public sealed class ConfirmEmailVerificationCommandHandler : IRequestHandler<ConfirmEmailVerificationCommand, bool>
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

    public async Task<bool> HandleAsync(ConfirmEmailVerificationCommand request, CancellationToken cancellationToken)
    {
        var storedCode = await _verificationCodeRepository.FindActiveAsync(
            request.UserId, VerificationCodePurpose.EmailVerification, cancellationToken);
        if (storedCode is null)
        {
            return false;
        }

        if (!_codeGenerator.Verify(request.Code, storedCode.CodeHash))
        {
            // Counted, so a six-digit code can't be ground down by repeated guessing - see UserVerificationCode.
            storedCode.RecordFailedAttempt();
            await _verificationCodeRepository.UpdateAsync(storedCode, cancellationToken);
            return false;
        }

        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return false;
        }

        storedCode.Consume();
        await _verificationCodeRepository.UpdateAsync(storedCode, cancellationToken);

        user.SetVerifiedEmail(storedCode.EmailAddress);
        await _userRepository.UpdateAsync(user, cancellationToken);
        return true;
    }
}
