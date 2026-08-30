using Orbit.Mobile.Screens.Account;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>The chosen accent, kept for as long as one test runs.</summary>
internal sealed class InMemoryAccentColorStore : IAccentColorStore
{
    public AccentColor Remembered { get; private set; } = AccentColor.Default;

    public AccentColor Read() => Remembered;

    public void Write(AccentColor accentColor) => Remembered = accentColor;
}
