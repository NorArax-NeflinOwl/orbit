# Components and dependencies

What the solution is made of, and which direction the arrows are allowed to point.

```mermaid
flowchart TD
    subgraph clients["Clients"]
        maui["Orbit.Maui<br/><i>net10.0-android, net10.0-ios</i><br/>XAML pages, platform code"]
        mobile["Orbit.Mobile<br/><i>net10.0</i><br/>view models, local store, sync"]
        web["Orbit.Web<br/><i>Blazor WebAssembly</i>"]
    end

    subgraph server["Server"]
        api["Orbit.Api<br/><i>ASP.NET Core, :8080</i><br/>endpoints, middleware, hosted services"]
        data["Orbit.Data<br/><i>EF Core, PostgreSQL</i>"]
        google["Orbit.GoogleIntegration<br/><i>ID-token verification</i>"]
    end

    subgraph shared["Shared"]
        core["Orbit.Core<br/><b>no project references at all</b><br/>domain, rules, ports, dispatcher"]
        contracts["Orbit.Contracts<br/><i>the wire: DTOs</i>"]
        localization["Orbit.Localization<br/><i>Polish translations</i>"]
    end

    maui --> mobile
    maui --> contracts
    mobile --> core
    mobile --> contracts
    mobile --> localization
    web --> core
    web --> contracts
    web --> localization
    api --> core
    api --> contracts
    api --> data
    api --> google
    data --> core
    google --> core
```

## The one rule the picture is drawn to show

**`Orbit.Core` references no other project.** Everything else points at it and it points at nothing,
which is what keeps the domain independent of EF Core, of ASP.NET, of MAUI and of the wire format. A
reference added *from* `Orbit.Core` to anything is the change worth stopping in review; the rest of the
graph is ordinary.

`Orbit.Core` declares its needs as interfaces — `INoteRepository`, `IEmailSender`,
`IPushNotificationSender`, `ILiveUpdatePublisher`, `IPasswordHasher` — and something outside supplies
them. That is the whole of the ports-and-adapters arrangement here, and it is why the same domain can be
compiled into a server that talks to PostgreSQL and into a phone that does not.

## What "shared" does and does not mean

This is the part a diagram usually gets wrong, so it is stated rather than implied.

**The command/query dispatcher is server-side only.** `IDispatcher` and every `IRequestHandler` run in
`Orbit.Api`. Neither client resolves a dispatcher — they call HTTP endpoints. So `Orbit.Core` is not a
shared *application* layer with two hosts; it is a shared *vocabulary and rulebook* with one host.

What the clients actually take from it is rules that must not be re-decided differently on each side —
`Orbit.Core.Sync`, `Orbit.Core.Permissions`, `Orbit.Core.Tasks`, `Orbit.Core.Inventories`,
`Orbit.Core.Suggestions`, `Orbit.Core.Notifications`. A permission the phone read differently from the
server, or a sync state it named differently, would be a disagreement no compiler could catch.

**The phone keeps its own model.** `Orbit.Mobile.Data` holds `LocalNote`, `LocalTaskList`,
`LocalCalendarEvent`, `LocalInventory`, `LocalChatMessage` and repositories over a local SQLite
database. Those are not implementations of `Orbit.Core`'s repository ports — they are a second store
with a shape of its own, because a phone has to answer while offline and a server never does. The two
are reconciled by the synchronisers rather than by sharing an interface (see [flows](flows.md)).

## `Orbit.Maui` and `Orbit.Mobile`, and why they are two

`Orbit.Sln` cannot carry a MAUI head into an ordinary test project, so everything that could otherwise
sit in the head — view models, the local store, the sync spine, crypto, the API client — lives in
`Orbit.Mobile`, which is a plain `net10.0` library and therefore testable. `Orbit.Maui` is left with the
XAML and the platform code, which is the part no unit test would reach anyway.

That split is why `tests/Orbit.Mobile.Tests` exists and `tests/Orbit.Maui.Tests` does not.

## Test projects

Left out of the diagram above to keep it about the shipped code:

| Project | References |
| --- | --- |
| `Orbit.Api.Tests` | `Orbit.Api`, `Orbit.Localization` |
| `Orbit.Web.Tests` | `Orbit.Web` |
| `Orbit.Mobile.Tests` | `Orbit.Mobile` |
