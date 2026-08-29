namespace Orbit.Core.Mobile;

/// <summary>
/// The two apps <c>Orbit.Maui</c> builds. They are versioned and released independently - a store review
/// can hold one back while the other ships - so every version decision is made per platform rather than
/// for "the mobile app" as a whole.
/// </summary>
public enum MobilePlatform
{
    Ios,
    Android
}
