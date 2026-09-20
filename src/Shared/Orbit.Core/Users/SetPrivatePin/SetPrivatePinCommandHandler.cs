using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.SetPrivatePin;

public sealed class SetPrivatePinCommandHandler : IRequestHandler<SetPrivatePinCommand, bool>
{
    /// <summary>
    /// How long a PIN may be, and how short. Four to eight digits: four is what a reader expects to be
    /// asked for, and something long enough to be a password should be the password. Digits only,
    /// because the phone raises a number pad for it and a letter typed there would be a PIN its owner
    /// could not enter again on their own phone.
    /// </summary>
    public const int ShortestPin = 4;

    /// <inheritdoc cref="ShortestPin"/>
    public const int LongestPin = 8;

    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public SetPrivatePinCommandHandler(IUserRepository userRepository, IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    /// <summary>
    /// False when the account is gone, has no password to prove anything with, gives the wrong one, or
    /// offers a PIN that is not four to eight digits. One answer for all of them: which of them it was
    /// is a question only somebody guessing would be asking.
    /// </summary>
    public async Task<bool> HandleAsync(SetPrivatePinCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user?.PasswordHash is not { } passwordHash || !_passwordHasher.Verify(request.Password, passwordHash))
        {
            return false;
        }

        if (request.NewPin is { } pin && !IsAPin(pin))
        {
            return false;
        }

        user.SetPrivatePin(request.NewPin is { } newPin ? _passwordHasher.Hash(newPin) : null);
        await _userRepository.UpdateAsync(user, cancellationToken);
        return true;
    }

    /// <inheritdoc cref="ShortestPin"/>
    public static bool IsAPin(string pin)
        => pin.Length >= ShortestPin && pin.Length <= LongestPin && pin.All(char.IsAsciiDigit);
}
