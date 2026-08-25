using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.ResetPassword;

public sealed class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, bool>
{
    private readonly IUserRepository _userRepository;
    private readonly IUserVerificationCodeRepository _verificationCodeRepository;
    private readonly IVerificationCodeGenerator _codeGenerator;
    private readonly IPasswordHasher _passwordHasher;

    public ResetPasswordCommandHandler(
        IUserRepository userRepository, IUserVerificationCodeRepository verificationCodeRepository,
        IVerificationCodeGenerator codeGenerator, IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _verificationCodeRepository = verificationCodeRepository;
        _codeGenerator = codeGenerator;
        _passwordHasher = passwordHasher;
    }

    public async Task<bool> HandleAsync(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var normalized = request.EmailOrUserName.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByEmailAsync(normalized, cancellationToken)
            ?? await _userRepository.GetByUserNameAsync(normalized, cancellationToken);
        if (user is null)
        {
            return false;
        }

        var storedCode = await _verificationCodeRepository.FindActiveAsync(
            user.Id, VerificationCodePurpose.PasswordReset, cancellationToken);
        if (storedCode is null)
        {
            return false;
        }

        if (!_codeGenerator.Verify(request.Code, storedCode.CodeHash))
        {
            storedCode.RecordFailedAttempt();
            await _verificationCodeRepository.UpdateAsync(storedCode, cancellationToken);
            return false;
        }

        storedCode.Consume();
        await _verificationCodeRepository.UpdateAsync(storedCode, cancellationToken);

        user.ChangePassword(_passwordHasher.Hash(request.NewPassword));
        await _userRepository.UpdateAsync(user, cancellationToken);
        return true;
    }
}
