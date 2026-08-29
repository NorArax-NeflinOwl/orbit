using Orbit.Core.Mobile;
using Orbit.Mobile.Authentication;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// Stands in for the system browser, which is the one part of signing in with Google that needs a
/// device. Answers with whatever <see cref="Result"/> holds - a callback carrying a code, one carrying
/// an error, or null for a reader who backed out.
/// </summary>
internal sealed class FakeSignInBrowser : IWebSignInBrowser
{
    public static readonly Uri Address = new("com.orbitmaui.android:/oauth2redirect");

    public string Platform => nameof(MobilePlatform.Android);

    public bool WasOpened { get; private set; }

    public IReadOnlyDictionary<string, string>? Result { get; init; } =
        new Dictionary<string, string> { ["code"] = "the-code" };

    public Uri CallbackAddressFor(string clientId) => Address;

    public Task<IReadOnlyDictionary<string, string>?> SignInAsync(
        Uri startAddress, CancellationToken cancellationToken = default)
    {
        WasOpened = true;
        return Task.FromResult(Result);
    }
}
