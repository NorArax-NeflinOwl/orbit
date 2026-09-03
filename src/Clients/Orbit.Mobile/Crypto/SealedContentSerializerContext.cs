using System.Text.Json.Serialization;
using Orbit.Contracts.Inventories;
using Orbit.Contracts.Notes;
using Orbit.Contracts.Tasks;

namespace Orbit.Mobile.Crypto;

/// <summary>
/// Source-generated serialization for what a private item hides from the server.
///
/// The property names it writes are what makes a note sealed here readable in a browser, so nothing may
/// rename them: no naming policy is set, which leaves them exactly as Orbit.Web's reflection-based
/// serializer spells them. SealedContentTests pins that, because getting it wrong produces content that
/// round-trips perfectly on the phone and cannot be opened anywhere else.
/// </summary>
[JsonSerializable(typeof(SealedNote))]
[JsonSerializable(typeof(SealedTaskList))]
[JsonSerializable(typeof(SealedInventory))]
internal sealed partial class SealedContentSerializerContext : JsonSerializerContext;
