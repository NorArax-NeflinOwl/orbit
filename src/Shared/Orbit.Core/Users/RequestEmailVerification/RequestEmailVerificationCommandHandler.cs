using Orbit.Core.Abstractions;
using Orbit.Core.Notifications;

namespace Orbit.Core.Users.RequestEmailVerification;

public sealed class RequestEmailVerificationCommandHandler
    : IRequestHandler<RequestEmailVerificationCommand, EmailVerificationRequestResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IUserVerificationCodeRepository _verificationCodeRepository;
    private readonly IVerificationCodeGenerator _codeGenerator;
    private readonly IEmailSender _emailSender;

    public RequestEmailVerificationCommandHandler(
        IUserRepository userRepository, IUserVerificationCodeRepository verificationCodeRepository,
        IVerificationCodeGenerator codeGenerator, IEmailSender emailSender)
    {
        _userRepository = userRepository;
        _verificationCodeRepository = verificationCodeRepository;
        _codeGenerator = codeGenerator;
        _emailSender = emailSender;
    }

    public async Task<EmailVerificationRequestResult> HandleAsync(
        RequestEmailVerificationCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return EmailVerificationRequestResult.UserNotFound;
        }

        // Normalized the same way registration normalizes it, so "A@B.com" and "a@b.com" are one address.
        var emailAddress = request.EmailAddress.Trim().ToLowerInvariant();
        var existing = await _userRepository.GetByEmailAsync(emailAddress, cancellationToken);
        if (existing is not null && existing.Id != user.Id)
        {
            return EmailVerificationRequestResult.EmailTaken;
        }

        // Issuing a new code retires any earlier one, so the most recent email is always the only one
        // that works - otherwise a user who requested twice would face two codes and no way to tell which.
        await _verificationCodeRepository.ConsumeAllAsync(user.Id, VerificationCodePurpose.EmailVerification, cancellationToken);

        var code = _codeGenerator.Generate();
        await _verificationCodeRepository.AddAsync(
            UserVerificationCode.Create(user.Id, VerificationCodePurpose.EmailVerification, _codeGenerator.Hash(code), emailAddress),
            cancellationToken);

        await _emailSender.SendAsync(
            emailAddress,
            "Confirm your Orbit email address",
            $"""
             Your Orbit confirmation code is {code}

             Enter it in Orbit to confirm this address. The code expires in {UserVerificationCode.Lifetime.TotalMinutes:0} minutes.

             If you didn't ask for this, you can ignore this email - nothing changes until the code is entered.
             """,
            cancellationToken);

        return EmailVerificationRequestResult.Sent;
    }
}
