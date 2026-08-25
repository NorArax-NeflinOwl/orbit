using Orbit.Core.Abstractions;

namespace Orbit.Core.Sharing.GetPublicSharedItem;

/// <summary>Answered without any authentication - the token is the whole of the access check.</summary>
public sealed record GetPublicSharedItemQuery(string Token) : IRequest<PublicSharedItem?>;
