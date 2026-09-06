# Domain model

The aggregates in `Orbit.Core`, and the two shapes almost all of them repeat.

## The repeated shapes

Notes, task lists, calendar events and inventories are four different things that answer the same three
questions — who may see this, who is holding it open, and can the server read it. Rather than draw those
fields four times, they are drawn once here and referred to below.

```mermaid
classDiagram
    class Shareable {
        <<facet>>
        +bool IsShared
        +string? SharedByUserName
        +bool IsSharedWithOthers
        +ShareAccessLevel AccessLevel
    }
    class Lockable {
        <<facet>>
        +Guid? LockedByUserId
        +string? LockedByUserName
        +DateTimeOffset? LockExpiresAtUtc
    }
    class Sealable {
        <<facet>>
        +bool IsPrivate
        +EncryptedPayload? EncryptedContent
    }
    class ShareAccessLevel {
        <<enumeration>>
        ReadOnly
        Share
        EditOnly
        CanEdit
    }
    class EncryptedPayload {
        +string Ciphertext
        +string Nonce
    }
    Shareable ..> ShareAccessLevel
    Sealable ..> EncryptedPayload
```

**`Sealable` is not a preference.** `IsPrivate` with an `EncryptedPayload` means the server holds
ciphertext and nothing else — there is no server-side view of that item's content, so no feature can be
added later that reads it. That is why private items never appear in anything the server has to
understand, such as a search the server performs or an assistant prompt it assembles.

**`ShareAccessLevel` is ordered but not linear.** `Share` is not "more than `ReadOnly` and less than
`CanEdit`" — it is the right to pass access on, which `EditOnly` deliberately lacks. `CanGrant` on the
enum decides what a holder may hand to somebody else, and it is the only place that question is
answered.

## The aggregates

```mermaid
classDiagram
    direction LR

    class Note {
        +Guid Id
        +Guid UserId
        +string Title
        +IReadOnlyList~NoteContentLine~ Content
        +bool IsPinned
        +ItemPriority Priority
    }
    class TaskList {
        +Guid Id
        +Guid UserId
        +string Title
        +string Description
        +bool IsCompleted
        +bool IsGroup
        +Guid? LinkedInventoryId
    }
    class TaskItem {
        +Guid Id
        +string Description
        +DateTimeOffset? DueDateUtc
        +bool IsCompleted
        +IReadOnlyList~Guid~ LinkedTaskListIds
        +IReadOnlyList~string~ Categories
    }
    class TaskItemSubject {
        +who, where and what
    }
    class TaskItemProduct {
        +what to restock
    }
    class TaskItemReminders
    class CalendarEvent {
        +Guid Id
        +Guid UserId
    }
    class CalendarEventDetails {
        +title, when, notes
    }
    class EventRecurrence {
        +RecurrenceFrequency Frequency
    }
    class EventLocation
    class Inventory {
        +Guid Id
        +Guid UserId
        +string Name
        +string Description
    }
    class InventoryItem {
        +Guid Id
        +Guid InventoryId
        +string Name
        +decimal Quantity
        +decimal? MinimumQuantity
        +InventoryUnit Unit
        +DateTimeOffset? ExpiryDate
        +bool IsCheckedRegularly
        +Guid? PendingRestockTaskListId
    }
    class User {
        +Guid Id
        +string Email
        +string UserName
        +string DisplayName
        +string? PasswordHash
        +string? GoogleSubjectId
        +bool KeepsThirdPartiesOut
    }
    class WrappedPrivateKey {
        +string CiphertextBase64
        +string NonceBase64
        +string SaltBase64
        +int Iterations
    }
    class UserPresence
    class UserLocation
    class ChatMessage {
        +Guid Id
        +Guid SenderUserId
        +Guid RecipientUserId
        +string CiphertextBase64
        +Guid? GroupId
        +bool IsEdited
    }

    Note "1" *-- "0..*" NoteContentLine
    TaskList "1" *-- "0..*" TaskItem
    TaskItem "1" *-- "1" TaskItemSubject
    TaskItem "1" *-- "0..1" TaskItemProduct
    TaskItem "1" *-- "1" TaskItemReminders
    TaskItem "0..*" --> "0..*" TaskList : links to
    TaskList "0..1" --> "0..1" Inventory : measured against
    CalendarEvent "1" *-- "1" CalendarEventDetails
    CalendarEventDetails "1" *-- "0..1" EventRecurrence
    CalendarEventDetails "1" *-- "0..1" EventLocation
    Inventory "1" *-- "0..*" InventoryItem
    InventoryItem "0..1" --> "0..1" TaskList : pending restock
    User "1" *-- "0..1" WrappedPrivateKey
    User "1" *-- "1" UserPresence
    User "1" *-- "0..1" UserLocation
    User "1" --> "0..*" Note : owns
    User "1" --> "0..*" TaskList : owns
    User "1" --> "0..*" CalendarEvent : owns
    User "1" --> "0..*" Inventory : owns
    ChatMessage "0..*" --> "1" User : sender
    ChatMessage "0..*" --> "1" User : recipient
```

`Note`, `TaskList`, `CalendarEvent` and `Inventory` each carry the `Shareable`, `Lockable` and
`Sealable` facets above in full. They are left off this diagram only so the relationships stay
readable.

## What `ChatMessage` does not have

It has no content field. `CiphertextBase64` is the whole of the message as far as this codebase is
concerned, and it is the only chat property no server-side code ever interprets — see
[flows](flows.md#chat-that-the-server-cannot-read) for how the key that opens it never reaches the
server.

This is worth seeing in a class diagram precisely because it is an absence. A reader looking for "where
is the message body handled" will not find it, and the reason is the design rather than an omission.

## The two loops between modules

Most modules are independent. Two are not, and both are deliberate:

- **A task list can be measured against an inventory** (`TaskList.LinkedInventoryId`), and an inventory
  item can point back at the restock task it is waiting on (`InventoryItem.PendingRestockTaskListId`,
  `PendingRestockTaskItemId`). That is a cycle in the data, and `InventoryTaskListCoordinator` plus
  `PendingRestockTaskResolver` exist to keep the two ends agreeing.
- **A task item links to other task lists** (`TaskItem.LinkedTaskListIds`), so completing an item can
  depend on lists elsewhere — `LinkedTaskCompletionResolver` is what decides whether that counts as
  done, and `TaskListLinkValidator` is what stops a link being made into a cycle.

## The dispatcher

Every operation on the above is a request object and a handler, resolved through one interface:

```mermaid
classDiagram
    class IRequest~TResponse~ {
        <<interface>>
    }
    class IRequestHandler~TRequest, TResponse~ {
        <<interface>>
        +HandleAsync(TRequest, CancellationToken) Task~TResponse~
    }
    class IDispatcher {
        <<interface>>
        +SendAsync~TResponse~(IRequest~TResponse~, CancellationToken) Task~TResponse~
    }
    class Dispatcher
    class LoggingDispatcher

    IDispatcher <|.. Dispatcher
    IDispatcher <|.. LoggingDispatcher
    LoggingDispatcher o-- IDispatcher : wraps
    IDispatcher ..> IRequest~TResponse~
    IRequestHandler~TRequest, TResponse~ ..> IRequest~TResponse~
```

`LoggingDispatcher` decorates rather than replaces, so every request is logged in one place instead of
each handler remembering to. **Only `Orbit.Api` resolves an `IDispatcher`** — see
[components](components.md#what-shared-does-and-does-not-mean).
