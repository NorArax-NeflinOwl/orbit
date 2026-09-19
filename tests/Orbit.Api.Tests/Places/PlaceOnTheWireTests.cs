using Orbit.Api.Places;
using Orbit.Core.Abstractions;
using Orbit.Core.Calendar;
using Orbit.Core.Places;
using Xunit;

namespace Orbit.Api.Tests.Places;

/// <summary>
/// What a place looks like on the wire - see PlaceEndpoints.ToDto, which is internal so this can ask.
///
/// It exists because a field can be stored, read back, and then quietly dropped on the way out: the
/// entry a place was made from was, for as long as places have been made from entries, and nothing
/// failed - the browser simply believed every place was kept by hand and made a second one on every
/// save (see TaskEntryPlaces). Found on 2026-09-19.
/// </summary>
public sealed class PlaceOnTheWireTests
{
    [Fact]
    public void A_place_carries_the_entry_it_was_made_from()
    {
        var entryId = Guid.NewGuid();
        var place = Place.Create(
            Guid.NewGuid(), "The new flat", "", Somewhere(), isPrivate: false, sourceTaskItemId: entryId);

        Assert.Equal(entryId, PlaceEndpoints.ToDto(place).SourceTaskItemId);
    }

    /// <summary>The sealed half travels as it is stored: the server has no key and never had one.</summary>
    [Fact]
    public void A_sealed_place_carries_its_sealed_half_and_nothing_readable()
    {
        var place = Place.Create(
            Guid.NewGuid(), "", "", new EventLocation(null, 0, 0),
            encryptedContent: new EncryptedPayload("sealed-bakery", "a-nonce"));

        var dto = PlaceEndpoints.ToDto(place);

        Assert.True(dto.IsPrivate);
        Assert.Equal("sealed-bakery", dto.EncryptedContent!.Ciphertext);
        Assert.Equal(string.Empty, dto.Name);
    }

    private static EventLocation Somewhere() => new("Piękna 1, Warszawa", 52.2297, 21.0122);
}
