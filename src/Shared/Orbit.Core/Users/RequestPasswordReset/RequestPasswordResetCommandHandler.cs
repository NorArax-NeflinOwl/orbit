using Orbit.Core.Abstractions;
using Orbit.Core.Notifications;

namespace Orbit.Core.Users.RequestPasswordReset;

public sealed class RequestPasswordResetCommandHandler : IRequestHandler<RequestPasswordResetCommand, bool>
{
    private readonly IUserRepository _userRepository;
    private readonly IUserVerificationCodeRepository _verificationCodeRepository;
    private readonly IVerificationCodeGenerator _codeGenerator;
    private readonly IEmailSender _emailSender;

    public RequestPasswordResetCommandHandler(
        IUserRepository userRepository, IUserVerificationCodeRepository verificationCodeRepository,
        IVerificationCodeGenerator codeGenerator, IEmailSender emailSender)
    {
        _userRepository = userRepository;
        _verificationCodeRepository = verificationCodeRepository;
        _codeGenerator = codeGenerator;
        _emailSender = emailSender;
    }

    /// <summary>Always returns true - see the command's comment for why the caller learns nothing else.</summary>
    public async Task<bool> HandleAsync(RequestPasswordResetCommand request, CancellationToken cancellationToken)
    {
        var user = await FindUserAsync(request.EmailOrUserName, cancellationToken);
        if (user is null || !user.IsEmailVerified)
        {
            return true;
        }

        await _verificationCodeRepository.ConsumeAllAsync(user.Id, VerificationCodePurpose.PasswordReset, cancellationToken);

        var code = _codeGenerator.Generate();
        await _verificationCodeRepository.AddAsync(
            UserVerificationCode.Create(user.Id, VerificationCodePurpose.PasswordReset, _codeGenerator.Hash(code), user.Email),
            cancellationToken);

        await _emailSender.SendAsync(
            user.Email,
            "Reset your Orbit password",
            $"""
             Your Orbit password reset code is {code}

             Enter it in Orbit to set a new password. The code expires in {UserVerificationCode.Lifetime.TotalMinutes:0} minutes.

             Note that setting a new password does not recover chat history encrypted under the old one -
             those messages stay unreadable, because Orbit's servers never had the key to them.

             If you didn't ask for this, you can ignore this email - your password stays as it is.
             """,
            cancellationToken);

        return true;
    }

    private async Task<User?> FindUserAsync(string emailOrUserName, CancellationToken cancellationToken)
    {
        var normalized = emailOrUserName.Trim().ToLowerInvariant();
        return await _userRepository.GetByEmailAsync(normalized, cancellationToken)
            ?? await _userRepository.GetByUserNameAsync(normalized, cancellationToken);
    }
}
