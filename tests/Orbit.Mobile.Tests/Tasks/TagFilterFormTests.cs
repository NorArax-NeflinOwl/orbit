using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Orbit.Contracts.Tasks;
using Orbit.Mobile.Api;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Tasks;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Tasks;

/// <summary>
/// Making a Tasks card filter on the phone - see TagFilterForm, the tasks screen's half of Orbit.Web's
/// TagFilterDialog. What the form sends is what the reader ticked, in the order the checklist shows it,
/// and a filter that could not be made says why rather than closing as though it had been.
/// </summary>
public sealed class TagFilterFormTests
{
    [Fact]
    public async Task Ticked_tags_a_new_one_and_the_and_switch_are_what_is_saved()
    {
        var server = StubHttpMessageHandler.Custom(async (request, cancellationToken) =>
        {
            var made = await request.Content!.ReadFromJsonAsync<CreateTaskTagFilterRequest>(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = JsonContent.Create(new TaskTagFilterDto(Guid.NewGuid(), made!.Tags, made.MatchesAll, DateTimeOffset.UtcNow))
            };
        });
        var store = new InMemoryStore();
        var form = new TagFilterForm(new TaskTagFilters(new TasksClient(server.ToHttpClient()), store), Translations());

        form.Open(["shopping", "home", "Shopping"]);
        Assert.Equal(["home", "shopping"], form.Tags.Select(tag => tag.Name));
        Assert.False(form.SaveCommand.CanExecute(null));

        form.Tags.Single(tag => tag.Name == "home").IsChosen = true;
        form.NewTag = "garden";
        form.AddTagCommand.Execute(null);
        form.ToggleMatchesAllCommand.Execute(null);
        await form.SaveCommand.ExecuteAsync(null);

        var sent = JsonSerializer.Deserialize<CreateTaskTagFilterRequest>(
            Assert.Single(server.ReceivedRequests).Body!, JsonSerializerOptions.Web)!;
        Assert.Equal(["home", "garden"], sent.Tags);
        Assert.True(sent.MatchesAll);
        Assert.False(form.IsOpen);
        Assert.Contains("home and garden", form.Message);
        Assert.Single(store.ReadFilters());
    }

    [Fact]
    public async Task Without_a_connection_the_form_stays_open_and_says_why()
    {
        var form = new TagFilterForm(
            new TaskTagFilters(new TasksClient(StubHttpMessageHandler.Unreachable().ToHttpClient()), new InMemoryStore()),
            Translations());
        form.Open(["home"]);
        form.Tags.Single().IsChosen = true;

        await form.SaveCommand.ExecuteAsync(null);

        Assert.True(form.IsOpen);
        Assert.Equal("That filter could not be saved. Making one needs a connection.", form.Message);
    }

    private static Translations Translations() => new(new InMemoryLanguageStore());

    private sealed class InMemoryStore : ITaskTagFilterStore
    {
        private IReadOnlyList<TaskTagFilterDto> _filters = [];
        private Guid? _chosen;

        public IReadOnlyList<TaskTagFilterDto> ReadFilters() => _filters;

        public void WriteFilters(IReadOnlyList<TaskTagFilterDto> filters) => _filters = filters;

        public Guid? ReadChosen() => _chosen;

        public void WriteChosen(Guid? filterId) => _chosen = filterId;
    }
}
