using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.SignInWithGoogle;

/// <summary>Signs in with a Google ID token, creating the account on first use. Null when the token isn't trustworthy.</summary>
public sealed record SignInWithGoogleCommand(string IdToken) : IRequest<User?>;
