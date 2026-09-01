using Orbit.Core;
using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.UpdateProfile;

public sealed class UpdateProfileCommandHandler : IRequestHandler<UpdateProfileCommand, UpdateProfileResult>
{
    private readonly IUserRepository _userRepository;

    public UpdateProfileCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<UpdateProfileResult> HandleAsync(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return UpdateProfileResult.UserNotFound;
        }

        // Normalized exactly as registration normalizes it, so case alone never makes a "different" login.
        var userName = request.UserName.Trim().ToLowerInvariant();
        var existing = await _userRepository.GetByUserNameAsync(userName, cancellationToken);
        if (existing is not null && existing.Id != user.Id)
        {
            return UpdateProfileResult.UserNameTaken;
        }

        user.ChangeUserName(StoredTextLimits.OrRefuse(userName, StoredTextLimits.UserName, "login"));
        user.ChangeDisplayName(
            StoredTextLimits.OrRefuse(request.DisplayName.Trim(), StoredTextLimits.DisplayName, "display name"));
        await _userRepository.UpdateAsync(user, cancellationToken);
        return UpdateProfileResult.Success;
    }
}
