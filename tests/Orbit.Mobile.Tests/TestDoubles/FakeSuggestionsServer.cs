using System.Net;
using System.Net.Http.Json;
using System.Web;
using Orbit.Contracts.Suggestions;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// Names the account already has. Answers whatever a test put in <see cref="Names"/> that contains what
/// was typed, which is close enough to the real trigram search for a screen test: what matters here is
/// what the field does with an answer, not how the answer was found.
/// </summary>
internal sealed class FakeSuggestionsServer : HttpMessageHandler
{
    /// <summary>What this account has, and how close each is to count as the same thing.</summary>
    public List<NameSuggestionDto> Names { get; } = [];

    public bool IsUnreachable { get; set; }

    /// <summary>What was last asked for, so a test can prove nothing is asked before it should be.</summary>
    public string? LastQuery { get; private set; }

    /// <summary>Which field the last lookup was for - what tells a title's strip from an item's.</summary>
    public string? LastKind { get; private set; }

    public int Lookups { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (IsUnreachable)
        {
            throw new HttpRequestException("No such host is known.");
        }

        Lookups++;
        var parameters = HttpUtility.ParseQueryString(request.RequestUri!.Query);
        var query = parameters["query"] ?? string.Empty;
        LastQuery = query;
        LastKind = parameters["kind"];

        var found = Names
            .Where(name => name.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            .ToList();

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(found) });
    }

    public HttpClient ToHttpClient() => new(this, disposeHandler: false) { BaseAddress = new Uri("https://orbit.example/") };
}
