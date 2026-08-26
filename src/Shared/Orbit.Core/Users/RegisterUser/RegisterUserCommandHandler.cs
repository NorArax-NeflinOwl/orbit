using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.RegisterUser;

public sealed class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, RegisterUserResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public RegisterUserCommandHandler(IUserRepository userRepository, IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<RegisterUserResult> HandleAsync(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        // Normalized so "a@b.com"/"A@B.com" and "alice"/"Alice" are treated as the same account both
        // here and at login.
        var email = request.Email.Trim().ToLowerInvariant();
        var userName = request.UserName.Trim().ToLowerInvariant();

        if (await _userRepository.GetByEmailAsync(email, cancellationToken) is not null)
        {
            return RegisterUserResult.Rejected(RegistrationRejection.EmailTaken);
        }

        if (await _userRepository.GetByUserNameAsync(userName, cancellationToken) is not null)
        {
            return RegisterUserResult.Rejected(RegistrationRejection.UserNameTaken);
        }

        var passwordHash = _passwordHasher.Hash(request.Password);
        var user = User.Create(email, userName, request.DisplayName, passwordHash);
        await _userRepository.AddAsync(user, cancellationToken);

        return RegisterUserResult.Success(user);
    }
}
