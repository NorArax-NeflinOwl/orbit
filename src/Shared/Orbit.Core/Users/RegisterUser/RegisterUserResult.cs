namespace Orbit.Core.Users.RegisterUser;

/// <summary>Which of the two unique fields registration collided with.</summary>
public enum RegistrationRejection
{
    EmailTaken,
    UserNameTaken
}

/// <summary>
/// Outcome of <see cref="RegisterUserCommand"/>: either the newly created user, or which of the two
/// unique fields was already spoken for.
///
/// The reason is an enum rather than the sentence it used to be, because both the API and the browser
/// have to act on it - and telling a reader which of the two to change is the whole point of refusing.
/// </summary>
public sealed class RegisterUserResult
{
    public User? User { get; }
    public RegistrationRejection? Rejection { get; }

    private RegisterUserResult(User? user, RegistrationRejection? rejection)
    {
        User = user;
        Rejection = rejection;
    }

    public static RegisterUserResult Success(User user) => new(user, null);

    public static RegisterUserResult Rejected(RegistrationRejection rejection) => new(null, rejection);
}
