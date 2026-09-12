using Orbit.Contracts.Calendar;
using Orbit.Contracts.Places;
using Microsoft.Extensions.Logging;

namespace Orbit.Web.Services;

/// <summary>One Location entry as the list was saved with it - what its kept place is made from.</summary>
/// <param name="Latitude">The point picked on the map for it, or null when only its words are known.</param>
public sealed record EntryWithAPlace(
    Guid ItemId, string Description, string Address, double? Latitude, double? Longitude, string Colour,
    string Priority);

/// <summary>
/// Keeps a place for every Location entry of a task list: the pin a Location entry could never have,
/// because an entry says where in words (TaskItem.Location) and only a Place holds a point.
///
/// Done here, after the list is saved, rather than by the server: the server has no point for an entry
/// - the words are all it was sent - and cannot seal a place, and this browser holds both the point
/// somebody picked and the key. So each save makes the places it has to, changes the ones whose entry
/// changed, and deletes the ones whose entry is gone or is no longer a Location entry. A place answers
/// to its entry by Place.SourceTaskItemId.
///
/// Sealed when the list is and open when it is not: the entry's address already sits readable on the
/// server in an open list, so sealing its place would guard nothing - and would need a key this browser
/// may not hold, which would mean no place at all.
///
/// Best effort from start to finish. The list has already been saved by the time this runs, and a place
/// that could not be made - Nominatim finding nothing for the words, the network dropping - is logged and
/// left for the next save rather than turned into an error about a save that worked.
/// </summary>
public sealed class TaskEntryPlaces(PlacesApiClient places, GeocodingApiClient geocoding, ILogger<TaskEntryPlaces> logger)
{
    public async Task KeepAsync(
        Guid taskListId, bool listIsPrivate, IReadOnlyList<EntryWithAPlace> entries,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PlaceDto> kept;
        try
        {
            kept = await places.GetPlacesAsync(cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Couldn't read the kept places to match task list {TaskListId}'s entries to", taskListId);
            return;
        }

        // The reader's own places made from this list's entries. One handed over by somebody else is
        // theirs to keep or not, whatever it was made from.
        var madeFromThisList = kept
            .Where(place => place.SourceTaskItemId is not null && !place.IsShared && place.TaskListIds.Contains(taskListId))
            .ToList();

        foreach (var entry in entries)
        {
            var existing = kept.FirstOrDefault(place => place.SourceTaskItemId == entry.ItemId && !place.IsShared);
            await KeepOneAsync(taskListId, listIsPrivate, entry, existing, cancellationToken);
        }

        var stillLocationEntries = entries.Select(entry => entry.ItemId).ToHashSet();
        foreach (var orphan in madeFromThisList.Where(place => !stillLocationEntries.Contains(place.SourceTaskItemId!.Value)))
        {
            await ForgetAsync(orphan, cancellationToken);
        }
    }

    private async Task KeepOneAsync(
        Guid taskListId, bool listIsPrivate, EntryWithAPlace entry, PlaceDto? existing, CancellationToken cancellationToken)
    {
        var address = entry.Address.Trim();
        if (await PointOfAsync(entry, address, existing, cancellationToken) is not { } point)
        {
            logger.LogInformation("No point found for task entry {ItemId}'s address, so it keeps no place yet", entry.ItemId);
            return;
        }

        // Named what the entry says, which is what the reader wrote to mean this place; the address when
        // the entry says nothing else.
        var name = entry.Description.Trim().Length > 0 ? entry.Description.Trim() : address;
        var request = new SavePlaceRequest(
            name, new EventLocationDto(address, point.Latitude, point.Longitude),
            Description: existing?.Description ?? string.Empty,
            Colour: entry.Colour,
            Priority: entry.Priority,
            TaskListIds: [taskListId],
            IsPrivate: listIsPrivate,
            SourceTaskItemId: entry.ItemId);

        try
        {
            if (existing is null)
            {
                await places.CreatePlaceAsync(request, cancellationToken);
            }
            else if (!SaysTheSame(existing, request))
            {
                await places.UpdatePlaceAsync(existing.Id, request, cancellationToken);
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            // InvalidOperationException is a sealed list and no key on this browser to seal its place
            // with - see PlacesApiClient.SealIfPrivateAsync. The entry is saved; its place waits.
            logger.LogWarning(exception, "Couldn't keep a place for task entry {ItemId}", entry.ItemId);
        }
    }

    /// <summary>
    /// Where the place goes: the point somebody picked on the map, if they did; the point the place
    /// already has, if the words have not changed since it was found; and otherwise the words looked up,
    /// which may find nothing - and nothing is the answer then, rather than a pin in the wrong country.
    /// </summary>
    private async Task<GeocodedPlace?> PointOfAsync(
        EntryWithAPlace entry, string address, PlaceDto? existing, CancellationToken cancellationToken)
    {
        if (entry.Latitude is { } latitude && entry.Longitude is { } longitude)
        {
            return new GeocodedPlace(latitude, longitude);
        }

        if (existing is { Where.Address: { } knownAddress } && knownAddress == address)
        {
            return new GeocodedPlace(existing.Where.Latitude, existing.Where.Longitude);
        }

        try
        {
            return await geocoding.FindPlaceAsync(address, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Couldn't look up task entry {ItemId}'s address", entry.ItemId);
            return null;
        }
    }

    /// <summary>Whether saving again would change nothing - so an untouched entry does not bump its place.</summary>
    private static bool SaysTheSame(PlaceDto existing, SavePlaceRequest request)
        => existing.Name == request.Name
            && existing.Where.Address == request.Where.Address
            && existing.Where.Latitude == request.Where.Latitude
            && existing.Where.Longitude == request.Where.Longitude
            && existing.Colour == request.Colour
            && existing.Priority == request.Priority
            && existing.IsPrivate == request.IsPrivate
            && existing.TaskListIds.SequenceEqual(request.TaskListIds ?? []);

    private async Task ForgetAsync(PlaceDto place, CancellationToken cancellationToken)
    {
        try
        {
            await places.DeletePlaceAsync(place.Id, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Couldn't remove the place kept for a task entry that is gone ({PlaceId})", place.Id);
        }
    }
}
