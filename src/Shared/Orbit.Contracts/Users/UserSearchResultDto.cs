namespace Orbit.Contracts.Users;

public sealed record UserSearchResultDto(Guid Id, string UserName, string DisplayName, string? PublicKeyBase64);
