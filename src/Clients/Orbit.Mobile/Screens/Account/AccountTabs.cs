using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Screens.Account;

/// <summary>The four sections of the account screen, in the order Orbit.Web's Options page lists them.</summary>
public enum AccountTab
{
    Account,
    Appearance,
    Preferences,
    Permissions,
    Debug
}

/// <summary>
/// How Orbit looks on this device. Held here rather than on the server because it is exactly that -
/// this device - and a phone in a pocket and a browser at a desk want different answers.
/// </summary>
public enum ChosenTheme
{
    /// <summary>Whatever the phone itself is set to, which is what Orbit did before there was a choice.</summary>
    System,
    Light,
    Dark
}

/// <summary>Remembers the chosen theme between launches - see ILanguageStore for the same shape.</summary>
public interface IThemeStore
{
    ChosenTheme Read();

    void Write(ChosenTheme theme);
}

/// <summary>
/// One of the three ways Orbit can look on this device, as the strip of them shows it: a named object
/// rather than the bare enum, because the strip shows what it is given and the enum's own name is
/// English, and it says whether it is the one in force so the strip can mark it.
/// </summary>
public sealed record ThemeChoice(ChosenTheme Value, string Name, bool IsChosen)
{
    public static string Describe(ChosenTheme theme, Translations translations) => theme switch
    {
        ChosenTheme.Light => translations["Light"],
        ChosenTheme.Dark => translations["Dark"],
        _ => translations["System"]
    };
}

/// <summary>One tab on the account screen: what it is called and whether it is the one showing.</summary>
public sealed record AccountTabRow(AccountTab Tab, string Name, bool IsChosen)
{
    public static string Describe(AccountTab tab, Translations translations) => tab switch
    {
        AccountTab.Account => translations["Account"],
        AccountTab.Appearance => translations["Appearance"],
        AccountTab.Preferences => translations["Preferences"],
        AccountTab.Permissions => translations["Permissions"],
        // Named as the browser names it, and as the permission that opens it is named.
        _ => translations["Debugger"]
    };
}
