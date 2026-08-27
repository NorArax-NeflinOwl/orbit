namespace Orbit.Localization;

/// <summary>
/// The languages Orbit's own interface is written in.
///
/// Shared by both clients rather than declared twice: the translations they read are the same
/// dictionary, so the set of languages has to be the same list.
/// </summary>
public enum AppLanguage
{
    English,
    Polish
}
