using System.Globalization;

namespace Orbit.Core.Notifications;

/// <summary>
/// What one notification says, and where it goes.
///
/// Each half is kept as a format and its arguments rather than as a finished sentence, because the two
/// are read by different things. A push notification is drawn by the browser's service worker or by
/// Android, neither of which can translate, so those get <see cref="Title"/> and <see cref="Body"/> -
/// English, because the server has no idea what language the reader has chosen: it is a preference each
/// device keeps for itself, and nothing ever tells the server about it. The feed inside the app is drawn
/// by the app, which does know, and gets the format and the arguments so it can say the same thing in
/// the reader's language.
///
/// The format is the English sentence with {0}-style holes in it, which is exactly what both clients'
/// dictionary is keyed by - Translations.Format on the phone, T.Format on the web. Nothing had to be
/// invented for them to translate this; the server only had to stop doing their half for them, which it
/// had been doing since before either client could translate anything.
/// </summary>
/// <param name="TitleFormat">
/// Usually a phrase with nothing in it that varies - "New message", "Overdue task" - and then it is its
/// own key. Being shared something is the exception: who shared it belongs in the heading.
/// </param>
/// <param name="BodyFormat">The English sentence, with {0} where a value goes.</param>
/// <param name="TitleArguments">
/// What fills the title's holes. Never translated - they are the reader's own words for their own
/// things, or somebody's name.
/// </param>
/// <inheritdoc cref="TitleArguments" path="/summary"/>
public sealed record PushNotificationPayload(
    string TitleFormat, IReadOnlyList<string> TitleArguments,
    string BodyFormat, IReadOnlyList<string> BodyArguments,
    string Url)
{
    /// <summary>A heading that says the same thing however it is read, and a body that does not.</summary>
    public PushNotificationPayload(
        string title, string bodyFormat, IReadOnlyList<string> bodyArguments, string url)
        : this(title, [], bodyFormat, bodyArguments, url)
    {
    }

    /// <summary>Neither half has anything in it that varies.</summary>
    public PushNotificationPayload(string title, string bodyFormat, string url)
        : this(title, [], bodyFormat, [], url)
    {
    }

    /// <inheritdoc cref="Body"/>
    public string Title => Render(TitleFormat, TitleArguments);

    /// <summary>
    /// The sentence in English, for whoever cannot translate it. Formatted invariantly: the arguments
    /// arrive already written out, so there is nothing left here for a culture to decide.
    /// </summary>
    public string Body => Render(BodyFormat, BodyArguments);

    private static string Render(string format, IReadOnlyList<string> arguments)
        => arguments.Count == 0
            ? format
            : string.Format(CultureInfo.InvariantCulture, format, [.. arguments]);
}
