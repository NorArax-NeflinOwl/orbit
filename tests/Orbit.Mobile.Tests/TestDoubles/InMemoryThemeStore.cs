using Orbit.Mobile.Screens.Account;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>The chosen theme, kept for as long as one test runs.</summary>
internal sealed class InMemoryThemeStore : IThemeStore
{
    private ChosenTheme _theme = ChosenTheme.System;

    public ChosenTheme Read() => _theme;

    public void Write(ChosenTheme theme) => _theme = theme;
}
