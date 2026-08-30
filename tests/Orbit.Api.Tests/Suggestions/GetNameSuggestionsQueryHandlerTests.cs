using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Suggestions;
using Orbit.Core.Suggestions.GetNameSuggestions;
using Xunit;

namespace Orbit.Api.Tests.Suggestions;

/// <summary>
/// The rules around the similarity search, which is the part that is not the database's. What this is
/// for is written on the query itself: offering a name somebody already has is a database question, and
/// answering it with a language model would be slower, dearer, and wrong more often - a model does not
/// know what is in this warehouse.
/// </summary>
public sealed class GetNameSuggestionsQueryHandlerTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly InMemoryNameSuggestionRepository _repository = new();

    [Fact]
    public async Task A_name_already_on_the_shelf_is_offered()
    {
        _repository.Add(NameSuggestionKind.InventoryItemName, "Mleko 2%", "Chleb");

        var suggestions = await AskAsync(NameSuggestionKind.InventoryItemName, "mleko");

        Assert.Contains(suggestions, suggestion => suggestion.Name == "Mleko 2%");
    }

    [Fact]
    public async Task What_was_already_typed_is_not_offered_back()
    {
        _repository.Add(NameSuggestionKind.InventoryItemName, "Chleb", "Chleb tostowy");

        var suggestions = await AskAsync(NameSuggestionKind.InventoryItemName, "Chleb");

        // The one result guaranteed to be useless, and the one most likely to come first.
        Assert.DoesNotContain(suggestions, suggestion => suggestion.Name == "Chleb");
        Assert.Contains(suggestions, suggestion => suggestion.Name == "Chleb tostowy");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("m")]
    public async Task Too_little_to_go_on_asks_nothing_at_all(string typed)
    {
        _repository.Add(NameSuggestionKind.InventoryItemName, "Mleko 2%");

        var suggestions = await AskAsync(NameSuggestionKind.InventoryItemName, typed);

        // One character matches nearly everything, so the list would be noise - and this runs on
        // keystrokes, so the query that is not worth making is the one not to make.
        Assert.Empty(suggestions);
        Assert.Empty(_repository.Queries);
    }

    [Fact]
    public async Task Each_field_reads_from_its_own_names()
    {
        _repository.Add(NameSuggestionKind.InventoryItemName, "Mleko 2%");
        _repository.Add(NameSuggestionKind.WarehouseName, "Mleczarnia");

        var forAProduct = await AskAsync(NameSuggestionKind.InventoryItemName, "mlek");

        // Offering a warehouse's name where a product's is being typed would be worse than offering
        // nothing: it reads as a real suggestion.
        Assert.DoesNotContain(forAProduct, suggestion => suggestion.Name == "Mleczarnia");
    }

    [Fact]
    public async Task The_same_thing_spelled_differently_scores_as_a_duplicate()
    {
        _repository.Add(NameSuggestionKind.InventoryItemName, "Mleko 2%");

        // The case this exists for: the same product typed with a space in it, which is a second row in
        // the warehouse unless somebody is told about the first one.
        var suggestions = await AskAsync(NameSuggestionKind.InventoryItemName, "Mleko 2 %");

        // What separates "you already have one of these" from "here is a completion" - see
        // GetNameSuggestionsQueryHandler.DuplicateSimilarity.
        var found = Assert.Single(suggestions);
        Assert.True(found.Similarity >= GetNameSuggestionsQueryHandler.DuplicateSimilarity);
    }

    private async Task<IReadOnlyList<NameSuggestion>> AskAsync(NameSuggestionKind kind, string typed)
        => await new GetNameSuggestionsQueryHandler(_repository)
            .HandleAsync(new GetNameSuggestionsQuery(_userId, kind, typed), CancellationToken.None);
}
