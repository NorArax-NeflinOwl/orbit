using System.Text.Json;
using Orbit.Contracts.Inventories;
using Orbit.Contracts.Notes;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Crypto;

/// <summary>
/// What a private note is sealed into, and that a browser could open it again.
///
/// The failure this exists to catch is the same shape as the one BrowserInteropTests catches for chat,
/// one layer up: a payload the phone seals and opens perfectly, and no browser can read. What is inside
/// the ciphertext is JSON, and the two clients agree about it only by writing the same property names -
/// Orbit.Web serializes with plain reflection and default options, the phone with a source-generated
/// context, so nothing but a test keeps them the same.
/// </summary>
public sealed class SealedContentTests
{
    private static readonly Guid Owner = Guid.Parse("11111111-0000-4000-8000-000000000001");

    private static readonly SealedNote Note = new(
        "Bank details",
        [new NoteContentLineDto("sort code", false, false), new NoteContentLineDto("call them", true, true)]);

    /// <summary>A shelf whose one item is filed under two things - see InventoryItemRequest.Categories.</summary>
    private static readonly SealedInventory Shelf = new(
        "Medicine cabinet",
        [new InventoryItemRequest(
            Id: null, "Flour", "Food", "Baking", 2, 1, "Kilogram", ExpiryDate: null, "None",
            IsCheckedRegularly: false, Categories: ["Baking", "Dry goods"])]);

    [Fact]
    public void A_sealed_note_is_written_exactly_as_the_browser_writes_it()
    {
        var fromThePhone = JsonSerializer.Serialize(Note, SealedContentSerializerContext.Default.SealedNote);

        // Reflection with default options is what Orbit.Web's PrivateContentSealer does, so this is the
        // browser's own answer rather than a copy of it kept in step by hand.
        Assert.Equal(JsonSerializer.Serialize(Note), fromThePhone);
    }

    /// <summary>
    /// The same guarantee for a private inventory, and worth its own case: an inventory's items are the
    /// one sealed payload that carries a whole DTO of its own, so a field added to InventoryItemRequest
    /// is a field the two clients have to agree about. Adding Categories beside Category is exactly that
    /// kind of change, and this is what would have caught it going wrong.
    /// </summary>
    [Fact]
    public void A_sealed_inventory_is_written_exactly_as_the_browser_writes_it()
    {
        var fromThePhone = JsonSerializer.Serialize(
            Shelf, SealedContentSerializerContext.Default.SealedInventory);

        Assert.Equal(JsonSerializer.Serialize(Shelf), fromThePhone);
    }

    [Fact]
    public async Task A_sealed_inventory_opens_again_with_everything_its_items_were_filed_under()
    {
        using var key = await PrivateContent.HoldingAKeyFor(Owner).UnlockAsync();

        var opened = key.Open(
            key.Seal(Shelf, SealedContentSerializerContext.Default.SealedInventory),
            SealedContentSerializerContext.Default.SealedInventory);

        Assert.NotNull(opened);
        Assert.Equal(["Baking", "Dry goods"], opened.Items[0].AllCategories);
    }

    [Fact]
    public async Task What_this_device_seals_it_can_open_again()
    {
        using var key = await PrivateContent.HoldingAKeyFor(Owner).UnlockAsync();

        var opened = key.Open(
            key.Seal(Note, SealedContentSerializerContext.Default.SealedNote),
            SealedContentSerializerContext.Default.SealedNote);

        // Field by field: the record holds a list, and two lists holding the same lines are not the
        // same object, which is all a record's own equality would compare.
        Assert.NotNull(opened);
        Assert.Equal(Note.Title, opened.Title);
        Assert.Equal(Note.Content, opened.Content);
    }

    /// <summary>
    /// A note sealed under a key pair that has since been replaced - a password reset does exactly this.
    /// Null rather than an exception, so a list can show one unreadable note instead of failing whole.
    /// </summary>
    [Fact]
    public async Task Content_sealed_under_a_key_pair_since_replaced_opens_as_nothing()
    {
        using var replacedKey = await PrivateContent.HoldingAKeyFor(Owner).UnlockAsync();
        using var currentKey = await PrivateContent.HoldingAKeyFor(Owner).UnlockAsync();

        var opened = currentKey.Open(
            replacedKey.Seal(Note, SealedContentSerializerContext.Default.SealedNote),
            SealedContentSerializerContext.Default.SealedNote);

        Assert.Null(opened);
    }

    [Fact]
    public async Task A_device_holding_no_key_refuses_to_seal_rather_than_inventing_one()
    {
        var sealer = PrivateContent.SignedInWithoutAKey(Owner);

        Assert.False(await sealer.HasKeyAsync());
        await Assert.ThrowsAsync<EncryptionKeyLockedException>(() => sealer.UnlockAsync());
    }

    [Fact]
    public async Task Nobody_signed_in_reads_as_holding_no_key()
    {
        Assert.False(await PrivateContent.WithoutAKey().HasKeyAsync());
    }
}
