using System.Text.Json.Serialization;
using Orbit.Contracts.Calendar;
using Orbit.Contracts.Inventories;
using Orbit.Contracts.Notes;
using Orbit.Contracts.Tasks;

namespace Orbit.Mobile.Data;

/// <summary>
/// Source-generated serialization for what the local store keeps as JSON. Release builds of the app
/// trim and AOT-compile, which strips the reflection System.Text.Json would otherwise need - a column
/// that round-trips in Debug and silently fails to in the shipped build is found late and painfully.
/// </summary>
[JsonSerializable(typeof(IReadOnlyList<NoteContentLineDto>))]
[JsonSerializable(typeof(IReadOnlyList<TaskItemDto>))]
[JsonSerializable(typeof(CalendarEventDetailsDto))]
[JsonSerializable(typeof(IReadOnlyList<InventoryItemRequest>))]
// When each batch on a shelf arrived, by its id - see LocalInventory.ItemArrivals.
[JsonSerializable(typeof(IReadOnlyDictionary<Guid, DateTimeOffset>))]
[JsonSerializable(typeof(IReadOnlyList<LocalChatGroupMember>))]
// What a copy was taken from, already rendered as lines - see ICopyableForEditing.CopyBaseLines.
[JsonSerializable(typeof(IReadOnlyList<string>))]
internal sealed partial class LocalStoreSerializerContext : JsonSerializerContext;
