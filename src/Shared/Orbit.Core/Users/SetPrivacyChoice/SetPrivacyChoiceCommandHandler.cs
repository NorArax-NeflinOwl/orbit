using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.SetPrivacyChoice;

public sealed class SetPrivacyChoiceCommandHandler(IUserRepository userRepository)
    : IRequestHandler<SetPrivacyChoiceCommand, bool>
{
    public async Task<bool> HandleAsync(SetPrivacyChoiceCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return false;
        }

        user.SetKeepsThirdPartiesOut(request.KeepsThirdPartiesOut);
        await userRepository.UpdateAsync(user, cancellationToken);
        return true;
    }
}
