using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Orbit.Contracts.Tags;
using Orbit.Core.Tags;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// Orbit's tag colours, in memory, behind a real <see cref="HttpMessageHandler"/> - see TagEndpoints and
/// SetTagColourCommandHandler for what it copies. It refuses what the server refuses (a colour that is not
/// "#rrggbb", a tag with no name) and keeps what the server keeps (one colour per tag whatever its case,
/// every colour in the answer to a set, an empty colour taking one away), so a phone that got either wrong
/// would be found out here rather than against the real thing.
/// </summary>
internal sealed class FakeTagsServer : HttpMessageHandler
{
    private readonly Dictionary<string, TagColourDto> _colours = new(StringComparer.Ordinal);

    /// <summary>True while the server is simply unreachable, as it is to a phone with no signal.</summary>
    public bool IsUnreachable { get; set; }

    /// <summary>Every colour set, as it arrived - so a test can say what the phone sent and when.</summary>
    public List<SetTagColourRequest> Sets { get; } = [];

    public IReadOnlyCollection<TagColourDto> Colours => _colours.Values;

    /// <summary>A colour set somewhere else - another phone, a browser.</summary>
    public void ColourElsewhere(string tag, string colour) => _colours[TagNames.KeyOf(tag)] = new TagColourDto(tag, colour);

    /// <summary>A colour taken away somewhere else.</summary>
    public void TakeAwayElsewhere(string tag) => _colours.Remove(TagNames.KeyOf(tag));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (IsUnreachable)
        {
            throw new HttpRequestException("No route to the server.");
        }

        if (request.RequestUri!.AbsolutePath != "/api/tags/colours")
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        if (request.Method == HttpMethod.Put)
        {
            var asked = JsonSerializer.Deserialize<SetTagColourRequest>(
                await request.Content!.ReadAsStringAsync(cancellationToken),
                new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            Sets.Add(asked);

            var tag = asked.Tag.Trim();
            var colour = asked.Colour.Trim();
            if (tag.Length == 0 || (colour.Length > 0 && !TagColour.IsAColour(colour)))
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }

            if (colour.Length == 0)
            {
                _colours.Remove(TagNames.KeyOf(tag));
            }
            else
            {
                _colours[TagNames.KeyOf(tag)] = new TagColourDto(tag, colour.ToLowerInvariant());
            }
        }

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(_colours.Values.ToList()) };
    }

    public HttpClient ToHttpClient() => new(this, disposeHandler: false) { BaseAddress = new Uri("https://orbit.example/") };
}
