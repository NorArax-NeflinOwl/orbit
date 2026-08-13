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
        // Normalized so "a@b.com" and "A@B.com" are treated as the same account both here and at login.
        var email = request.Email.Trim().ToLowerInvariant();

        var existingUser = await _userRepository.GetByEmailAsync(email, cancellationToken);
        if (existingUser is not null)
        {
            return RegisterUserResult.Failure("An account with this email address already exists.");
        }

        var passwordHash = _passwordHasher.Hash(request.Password);
        var user = User.Create(email, request.DisplayName, passwordHash);
        await _userRepository.AddAsync(user, cancellationToken);

        return RegisterUserResult.Success(user);
    }
}
