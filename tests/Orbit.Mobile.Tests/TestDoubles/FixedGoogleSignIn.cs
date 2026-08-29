using Orbit.Mobile.Authentication;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// Google answering however the test says, which no test can arrange for real - the real one opens a
/// browser sheet and waits for a redirect back into the app.
/// </summary>
internal sealed class FixedGoogleSignIn : IGoogleSignIn
{
    public GoogleSignInResult Result { get; set; } = GoogleSignInResult.Succeeded("an-id-token");

    /// <summary>False stands for a build with no client id, which is what hides the button.</summary>
    public bool IsConfigured { get; set; } = true;

    public int RequestCount { get; private set; }

    public Task<GoogleSignInResult> RequestIdTokenAsync(CancellationToken cancellationToken = default)
    {
        RequestCount++;
        return Task.FromResult(IsConfigured ? Result : GoogleSignInResult.NotConfigured);
    }
}
