# Database

Forty-one tables in PostgreSQL. Split into five diagrams here, because one picture of all of them would
be a wall rather than a drawing.

## Reading a name

No table or column is left to EF Core's defaults. `Orbit.Data.OrbitStorageNames` holds the physical name
of every one of them and applies it at the end of `OnModelCreating`; **an entity missing from that map
throws at startup** rather than quietly taking a default and drifting out of the convention.

| Prefix | Holds | Example |
| --- | --- | --- |
| `OP_` | what the user works on | `OP_NOTES`, `OP_INVENTORIES_ITEMS` |
| `OL_` | rows whose only job is to join two of those | `OL_CONTACTS`, `OL_PUBLIC_SHARES` |
| `OS_` | accounts, settings, and bookkeeping the system keeps for itself | `OS_USERS`, `OS_RATE_LIMITS` |

A column repeats its table's prefix, shortens the middle to initials and ends with the property name in
upper case: `OP_NOTES.OP_N_TITLE`, `OP_NOTES_SHARED.OP_NS_ACCESSLEVEL`. So a column carries its table
with it in a query that joins several.

The diagrams below use the physical names, since those are what a query is written against.

## Accounts and getting in

```mermaid
erDiagram
    OS_USERS ||--o{ OS_REFRESH_TOKENS : "issued"
    OS_USERS ||--o{ OS_VERIFICATION_CODES : "sent"
    OS_USERS ||--o{ OS_USERS_PERMISSIONS : "holds"
    OS_USERS ||--o| OS_NOTIFICATIONS_SETTINGS : "configures"
    OS_USERS ||--o{ OS_PUSH_SUBSCRIPTIONS : "registered"

    OS_USERS {
        uuid OS_U_ID PK
        text OS_U_EMAIL
        text OS_U_USERNAME
        text OS_U_PASSWORDHASH "null when Google-only"
        text OS_U_GOOGLESUBJECTID
        text OS_U_PUBLICKEYBASE64 "E2EE, published"
        text OS_U_WRAPPEDPRIVATEKEYBASE64 "E2EE, password-wrapped backup"
        text OS_U_PRIVATEKEYSALTBASE64
        int OS_U_PRIVATEKEYDERIVATIONITERATIONS
        bool OS_U_KEEPSTHIRDPARTIESOUT
        timestamptz OS_U_EMAILVERIFIEDATUTC
    }
    OS_REFRESH_TOKENS {
        uuid OS_RT_ID PK
        uuid OS_RT_USERID FK
        text OS_RT_TOKENHASH "hashed, never the token"
        timestamptz OS_RT_EXPIRESATUTC
        timestamptz OS_RT_REVOKEDATUTC
    }
    OS_VERIFICATION_CODES {
        uuid OS_VC_ID PK
        uuid OS_VC_USERID FK
        text OS_VC_PURPOSE
        text OS_VC_CODEHASH "hashed, never the code"
        int OS_VC_FAILEDATTEMPTS
        timestamptz OS_VC_CONSUMEDATUTC
    }
    OS_USERS_PERMISSIONS {
        uuid OS_UP_USERID PK,FK
        text OS_UP_PERMISSION PK
    }
    OS_PERMISSIONS_CODES {
        text OS_PC_PERMISSION PK "one code per permission"
        text OS_PC_CODE
    }
```

Two columns worth naming: `OS_RT_TOKENHASH` and `OS_VC_CODEHASH` store hashes, never the token or the
code. A database that leaked would hand over neither.

`OS_PERMISSIONS_CODES` is keyed on the permission itself, so a second code can never be minted beside
the one somebody was already told.

## The four things a user works on, and how they are shared

Notes, task lists, calendar events and inventories each have a table and a share table beside it with
the same shape.

```mermaid
erDiagram
    OS_USERS ||--o{ OP_NOTES : owns
    OS_USERS ||--o{ OP_TASKS : owns
    OS_USERS ||--o{ OP_EVENTS : owns
    OS_USERS ||--o{ OP_INVENTORIES : owns

    OP_NOTES ||--o{ OP_NOTES_SHARED : "shared as"
    OP_TASKS ||--o{ OP_TASKS_SHARED : "shared as"
    OP_EVENTS ||--o{ OP_EVENTS_SHARED : "shared as"
    OP_INVENTORIES ||--o{ OP_INVENTORIES_SHARED : "shared as"
    OS_USERS ||--o{ OL_PUBLIC_SHARES : "published"

    OP_NOTES {
        uuid OP_N_ID PK
        uuid OP_N_USERID FK
        text OP_N_TITLE
        bool OP_N_ISPRIVATE
        text OP_N_ENCRYPTEDCIPHERTEXT "set when private"
        text OP_N_ENCRYPTEDNONCE
        text OP_N_CONTENTJSON "null when private"
        uuid OP_N_LOCKEDBYUSERID
        timestamptz OP_N_LOCKEXPIRESATUTC
    }
    OP_NOTES_SHARED {
        uuid OP_NS_ID PK
        uuid OP_NS_SOURCENOTEID FK
        uuid OP_NS_OWNERUSERID FK
        uuid OP_NS_RECIPIENTUSERID FK
        text OP_NS_ACCESSLEVEL
        timestamptz OP_NS_ACCEPTEDATUTC "null until accepted"
    }
    OL_PUBLIC_SHARES {
        uuid OL_PS_ID PK
        text OL_PS_TOKEN "the whole access check"
        uuid OL_PS_OWNERUSERID FK
        text OL_PS_ITEMTYPE "SharedItemType, stored by name"
        uuid OL_PS_ITEMID
        timestamptz OL_PS_REVOKEDATUTC
    }
```

`OP_TASKS_SHARED`, `OP_EVENTS_SHARED` and `OP_INVENTORIES_SHARED` are the same five columns over a
different source id, so only one is drawn.

**`OL_PS_ITEMTYPE` stores an enum by name and must never be renamed.** It sits in rows already written
and inside chat payloads already delivered; renaming the member orphans every share link that used it.
That is why the enum still says `Warehouse` although everything else says Inventory.

## Tasks and inventories, and the loop between them

```mermaid
erDiagram
    OP_TASKS ||--o{ OP_TASKS_ITEMS : contains
    OP_TASKS_ITEMS ||--o{ OP_TASKS_CATEGORIES : "tagged"
    OP_TASKS_ITEMS ||--o{ OP_TASKS_PRODUCT_CATEGORIES : "product tagged"
    OP_TASKS_ITEMS ||--o{ OL_TASKS_ITEMS : "links to lists"
    OP_TASKS ||--o{ OL_TASKS_ITEMS : "linked from items"
    OP_INVENTORIES ||--o{ OP_INVENTORIES_ITEMS : contains
    OP_INVENTORIES_ITEMS ||--o{ OP_INVENTORIES_CATEGORIES : "tagged"
    OP_INVENTORIES ||--o{ OL_INVENTORIES_TASKS : "restocked through"
    OP_TASKS ||--o{ OL_INVENTORIES_TASKS : "restocks"

    OP_TASKS_ITEMS {
        uuid OP_TI_ID PK
        uuid OP_TI_TASKID FK
        int OP_TI_POSITION
        text OP_TI_DESCRIPTION
        timestamptz OP_TI_DUEDATEUTC
        bool OP_TI_ISCOMPLETED
        text OP_TI_KIND
        uuid OP_TI_LINKEDCALENDAREVENTID
        uuid OP_TI_LINKEDINVENTORYITEMID
        bool OP_TI_REMINDDAILY
    }
    OP_INVENTORIES_ITEMS {
        uuid OP_II_ID PK
        uuid OP_II_INVENTORYID FK
        text OP_II_NAME
        numeric OP_II_QUANTITY
        numeric OP_II_MINIMUMQUANTITY
        text OP_II_UNIT
        date OP_II_EXPIRYDATE
        bool OP_II_ISCHECKEDREGULARLY
        uuid OP_II_PENDINGRESTOCKTASKLISTID "points back at a task"
        uuid OP_II_PENDINGRESTOCKTASKITEMID
    }
    OL_INVENTORIES_TASKS {
        uuid OL_IT_ID PK
        uuid OL_IT_INVENTORYID FK
        uuid OL_IT_TASKLISTID FK
        bool OL_IT_ISENABLED
        int OL_IT_REFRESHTIMEOFDAYMINUTES
        bool OL_IT_ONLYCHECKEDREGULARLY
    }
    OL_TASKS_ITEMS {
        uuid OL_TI_TASKITEMID PK,FK
        uuid OL_TI_LINKEDTASKLISTID PK,FK
        int OL_TI_POSITION
    }
```

**This is the one cycle in the schema.** An inventory names the task list that restocks it, and an
inventory item names the task it is currently waiting on. Both directions are needed — one answers "what
should this shopping list contain", the other "has this been bought yet" — and
`InventoryTaskListCoordinator` is the single place that keeps the two ends agreeing.

`OL_TASKS_ITEMS` is the item-to-list link, and is why a task item can stand for work tracked on other
lists. `TaskListLinkValidator` is what stops one being made into a cycle of its own.

## Chat, which the server stores but cannot read

```mermaid
erDiagram
    OS_USERS ||--o{ OL_CONTACTS : keeps
    OS_USERS ||--o{ OL_CHATS_ACCESS : "asked or was asked"
    OS_USERS ||--o{ OP_CHATS : sent
    OP_CHATS_GROUPS ||--o{ OL_CHATS_MEMBERS : has
    OP_CHATS_GROUPS ||--o{ OP_CHATS : carries
    OP_CHATS_GROUPS ||--o{ OP_CHATS_ANNOUNCEMENTS : announces
    OS_USERS ||--o{ OP_LOCATIONS : shares

    OP_CHATS {
        uuid OP_C_ID PK
        uuid OP_C_SENDERUSERID FK
        uuid OP_C_RECIPIENTUSERID FK
        text OP_C_CIPHERTEXTBASE64 "the entire message"
        timestamptz OP_C_SENTATUTC
        timestamptz OP_C_READATUTC
        uuid OP_C_GROUPID FK
        bool OP_C_ISSHAREDHISTORY
    }
    OL_CHATS_ACCESS {
        uuid OL_CA_ID PK
        uuid OL_CA_INITIATEDBYUSERID FK
        uuid OL_CA_OTHERUSERID FK
        timestamptz OL_CA_APPROVEDATUTC "null until approved"
    }
    OL_CONTACTS {
        uuid OL_C_ID PK
        uuid OL_C_OWNERUSERID FK
        uuid OL_C_CONTACTUSERID FK
        bool OL_C_ISARCHIVED
        timestamptz OL_C_HISTORYCLEAREDATUTC
    }
    OL_CHATS_MEMBERS {
        uuid OL_CM_ID PK
        uuid OL_CM_GROUPID FK
        uuid OL_CM_USERID FK
        text OL_CM_ROLE
    }
    OP_LOCATIONS {
        uuid OP_L_ID PK
        uuid OP_L_SHARERUSERID FK
        uuid OP_L_RECIPIENTUSERID FK
        bool OP_L_ISCONTINUOUS
    }
```

`OP_CHATS` has **no column for message content**. `OP_C_CIPHERTEXTBASE64` is the message, and nothing on
the server can open it — see [flows](flows.md#chat-that-the-server-cannot-read).

`OL_CHATS_ACCESS` is the row that makes a conversation a conversation: until
`OL_CA_APPROVEDATUTC` is set, one person has asked and the other has not agreed.

## Bookkeeping

Nothing here is the user's; all of it is the system's own record of what it has already done.

```mermaid
erDiagram
    OS_USERS ||--o{ OP_NOTIFICATIONS : "feed"
    OS_USERS ||--o{ OS_SYNC_TOMBSTONES : "deleted"
    OS_USERS ||--o{ OS_DIAGNOSTICS : "uploaded"
    OP_EVENTS ||--o{ OS_EVENTS_REMINDERS : "reminded"
    OP_TASKS_ITEMS ||--o{ OS_TASKS_REMINDERS : "reminded daily"
    OP_TASKS_ITEMS ||--o{ OS_TASKS_OVERDUE : "reported overdue"
    OP_INVENTORIES_ITEMS ||--o{ OS_INVENTORIES_EXPIRY : "reported expiring"

    OS_SYNC_TOMBSTONES {
        uuid OS_ST_ID PK
        uuid OS_ST_USERID FK
        text OS_ST_ENTITYTYPE
        uuid OS_ST_ENTITYID
        timestamptz OS_ST_DELETEDATUTC
    }
    OS_EVENTS_REMINDERS {
        uuid OS_ER_ID PK
        uuid OS_ER_CALENDAREVENTID FK
        int OS_ER_MINUTESBEFORESTART
        timestamptz OS_ER_OCCURRENCESTARTUTC
        timestamptz OS_ER_SENTATUTC
    }
    OS_RATE_LIMITS {
        text OS_RL_PARTITION PK
        timestamptz OS_RL_WINDOWSTART PK
        int OS_RL_COUNT
    }
```

**The four delivery tables are claims, not logs.** A row is inserted *before* a notification is sent,
under a unique index, so an instance whose insert loses the race knows another instance has already
claimed that notification. That is what makes the reminder background services safe to run on more than
one replica — see [flows](flows.md).

**`OS_SYNC_TOMBSTONES` is why a delete propagates.** A row that is simply gone is indistinguishable from
one a device has not fetched yet, so a deletion leaves a marker with a timestamp and the device learns
about it from its cursor like any other change.

**`OS_RATE_LIMITS`** is keyed on caller and window together, which is what lets taking a permit be one
`INSERT ... ON CONFLICT DO UPDATE` and therefore safe between replicas.

## Migrations

EF Core generates them and they are reviewed by hand before they are kept — see
[testing-and-running-locally](../testing-and-running-locally.md#database-migrations). The rule that has
caught real damage: **a rename must be written as `RenameTable`/`RenameColumn`.** Left to scaffold it,
EF emits drop-and-create, which is data loss wearing the clothes of a schema change.
