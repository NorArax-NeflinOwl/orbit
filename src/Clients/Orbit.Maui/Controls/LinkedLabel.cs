using Orbit.Core.Text;

namespace Orbit.Maui.Controls;

/// <summary>
/// A label whose web addresses can be pressed instead of copied out by hand - the browser's own
/// TextWithLinks, on a phone. The rule about what counts as an address is shared rather than written
/// twice: <see cref="LinksInText"/> lives in Orbit.Core, and only http and https ever become something
/// to press (see its comment for why that is a list of what is allowed rather than of what is not).
///
/// A Label rather than a control wrapping one, so it drops into the places a Label already sits with
/// their styles, sizes and wrapping intact. It writes <see cref="Label.FormattedText"/>, which is the
/// only way MAUI lets part of a line be pressed - a Span carries its own gestures, and Text does not.
/// The words go in through <see cref="Words"/> rather than through Text for the same reason: setting
/// both would be two answers about what the label says.
/// </summary>
public sealed class LinkedLabel : Label
{
    public static readonly BindableProperty WordsProperty =
        BindableProperty.Create(nameof(Words), typeof(string), typeof(LinkedLabel), string.Empty,
            propertyChanged: (label, _, _) => ((LinkedLabel)label).Redraw());

    /// <summary>
    /// What was written, addresses and all. Empty draws an empty line rather than nothing, which is
    /// what a Label showing an empty string already does.
    /// </summary>
    public string Words
    {
        get => (string)GetValue(WordsProperty);
        set => SetValue(WordsProperty, value);
    }

    private void Redraw()
    {
        var runs = LinksInText.Split(Words);
        var formatted = new FormattedString();
        foreach (var run in runs)
        {
            var span = new Span { Text = run.Text };
            if (run.Url is { } url)
            {
                span.TextDecorations = TextDecorations.Underline;
                span.SetDynamicResource(Span.TextColorProperty, "Accent");
                // Opened in whatever the phone opens links with, and outside the app: following a link
                // out of a half-written message is the one thing nobody meant to do, so the message
                // stays where it was. A failure is swallowed - a phone with nothing to open it with has
                // nothing to say about it either, and the words are still on screen.
                span.GestureRecognizers.Add(new TapGestureRecognizer
                {
                    Command = new Command(async () =>
                    {
                        try
                        {
                            await Launcher.Default.OpenAsync(url);
                        }
                        catch (Exception exception) when (exception is not OperationCanceledException)
                        {
                        }
                    })
                });
            }

            formatted.Spans.Add(span);
        }

        FormattedText = formatted;
    }
}
