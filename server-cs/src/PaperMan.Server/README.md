# PaperMan.Server source guide

**Boundary — Fact/HIGH.** This assembly owns the executable host, TCP/UDP
transport lifetime, received-packet state gates, process-local room/session
state, and SQLite-backed private-server state. It consumes byte-exact values
from `PaperMan.Protocol`; it does not redefine Packet framing, AES, LZ, or the
generated opcode catalog.

**Evidence convention.** A native function, packet layout, or state statement
is Fact/HIGH only where the linked code/docs establish it. `Raw`, `Reserved`,
`Opaque`, and `UNRESOLVED` retain their stated uncertainty. A source directory
is an organization decision, never evidence that the original service had the
same subsystem boundary.

## Read the runtime in this order

```text
Host/Program.cs
  -> Host/Session.cs          one TCP connection's frame receive/send lifetime
  -> Host/Router.cs           role/state gate and registered opcode lookup
  -> Handlers/<family>/       canonical request-token entry
  -> Database/ or State/      explicit mutation owner
  -> Protocol contract/codec  exact payload and wire representation
```

For a known packet, begin with `db/packets.tsv` and generated
`PaperMan.Protocol/Generated/Opcode.cs`, then use `Host/Router.cs` to find the
family registry and finally the exact `Handlers.<TOKEN-without-_REQ>.cs` file.
A source path never changes the canonical filename or receive-entry token.

## Directory ownership

| Directory | Start here | Owns | Does not own |
|---|---|---|---|
| [`Host/`](Host/) | `Program.cs` | zero-argument startup, listener/session lifetime, role/state router, server configuration, and the narrow UDP endpoint | packet codec internals, room policy, or SQLite domain operations |
| [`State/`](State/) | `Room.cs`, `RoomManager.cs` / registries | process-local room, battle, session, and one-use channel-admission state | durable account/item data or unproven client service policy |
| [`Database/`](Database/) | `Db.Connection.cs`, `DatabaseBootstrapper.cs` | SQLite connection/bootstrap and each persisted-domain `Db.*` partial | socket/session ownership or Packet serialization |
| [`Handlers/`](Handlers/) | family `Handlers.<Family>.Registry.cs` | direct canonical receive entries and source-proven response/state behavior | generic service frameworks or renamed business aliases |

`Host/`, `State/`, and `Database/` keep ordinary server infrastructure separate
from packet behavior. `Handlers/` is subdivided only by the existing
binding-only registries, so a directory is backed by an explicit Router path
rather than a guessed gameplay taxonomy.

## State and database file map

| Concern | File(s) | Boundary |
|---|---|---|
| Room model / live lifecycle | `State/Room.cs`, `State/RoomBattleState.cs`, `State/RoomManager.cs` | room configuration/seats, mode-specific battle state, then manager lookup/broadcast lifecycle |
| Other process-local state | `State/SessionRegistry.cs`, `State/ChannelAdmissionRegistry.cs` | connected-session lookup and single-use 681→143 admission only |
| SQLite foundation | `Database/Db.Connection.cs`, `Database/DatabaseBootstrapper.cs` | connection, migrations, shared SQL helpers, schema/catalog/config bootstrap |
| Account and first-player bootstrap | `Database/Db.Accounts.cs`, `Database/Db.PlayerBootstrap.cs` | credential/account lookup, canonical starter identity/nickname/appearance rows |
| Player read/mutation projections | `Database/Db.PlayerInfo.cs`, `Database/Db.Characters.cs`, `Database/Db.PlayerProgress.cs` | my-info counters, character rows, selected character/tutorial/monotonic totals |
| Loadout and NewSkill state | `Database/Db.Loadouts.cs`, `Database/Db.WeaponLoadout.cs`, `Database/Db.NewSkillProfiles.cs` | read projection, source-bounded weapon update, five-record NewSkill state |
| Items and player inbox | `Database/Db.Inventory.cs`, `Database/Db.Warehouse.cs`, `Database/Db.Gifts.cs`, `Database/Db.Mailbox.cs` | each named durable item/inbox store; no inferred cross-store policy |
| Other persisted domains | `Database/Db.Friends.cs`, `Database/Db.Quests.cs`, `Database/Db.Clans.cs`, `Database/Db.Voice.cs`, `Database/Db.GameCenter.cs`, `Database/Db.Rooms.cs`, `Database/Db.PacketStats.cs` | the named narrow durable operation, not a generic service layer |

The filename map is a reading aid. It does not elevate a local persistence
boundary into evidence of an original production-service boundary.

## Handler family map

| Directory | Binding-only registry / purpose |
|---|---|
| [`Handlers/Auth/`](Handlers/Auth/) | `Handlers.Auth.Registry.cs`: `GT_PING`, `GL_LOGIN` |
| [`Handlers/Channel/`](Handlers/Channel/) | `Handlers.Channel.Registry.cs`: 143, 195, 196, 141 channel bootstrap flow |
| [`Handlers/Join/`](Handlers/Join/) | `Handlers.Join.Registry.cs`: lobby-to-room join flow |
| [`Handlers/Lobby/`](Handlers/Lobby/) | `Handlers.Lobby.Registry.cs`: lobby/user/item/client settings families |
| [`Handlers/Room/`](Handlers/Room/) | `Handlers.Room.Registry.cs`: room membership, settings, lifecycle, and in-room relay |
| [`Handlers/Battle/`](Handlers/Battle/) | battle + battle-object registries; the two explicitly named support files have no receive entry |
| [`Handlers/Ai/`](Handlers/Ai/) | `Handlers.Ai.Registry.cs`: AI/PvE request families |
| [`Handlers/Shop/`](Handlers/Shop/) | `Handlers.Shop.Registry.cs`: shop/Pepachi/capsule paths and the documented raw-206 exception |
| [`Handlers/Stats/`](Handlers/Stats/) | `Handlers.Stats.Registry.cs`: `GP_CH*C` totals and server-push support |
| [`Handlers/Friend/`](Handlers/Friend/) | `Handlers.Friend.Registry.cs`: friend and mailbox families |
| [`Handlers/Clan/`](Handlers/Clan/) | `Handlers.Clan.Registry.cs`: clan create/tunnel/tournament entry |
| [`Handlers/Quest/`](Handlers/Quest/) | `Handlers.Quest.Registry.cs`: quest/date families |
| [`Handlers/Voice/`](Handlers/Voice/) | `Handlers.Voice.Registry.cs`: voice-slot families |
| [`Handlers/Warehouse/`](Handlers/Warehouse/) | `Handlers.Warehouse.Registry.cs`: warehouse list/push/pop families |
| [`Handlers/GameCenter/`](Handlers/GameCenter/) | `Handlers.GameCenter.Registry.cs`: game-center families |
| [`Handlers/Master/`](Handlers/Master/) | `Handlers.Master.Registry.cs`: operator command namespace |

## Change checklist

1. Trace `catalog token → Router gate → registry binding → direct handler →
   packet consumer → state/SQLite mutation → response → next legal state`.
2. Preserve canonical opcode spelling in basename and method name. In
   particular, do not normalize catalog typos such as `WAREHOSUE`, invent a
   request name for raw opcode 206, or trim the non-`_REQ` `GL_MYINFO_OPEN`.
3. Keep a handler's role/session guard, exact length/count branches, and
   fail-closed unresolved boundary visible near its mutation. Do not replace an
   unknown response with zero padding or nominal success.
4. Treat `Extracted/` names as client lookup/UI evidence only. They do not
   prove server grants, pricing, ownership, routing, or persistence.
5. Run available static/Python checks. This Arena environment has no .NET SDK,
   so a .NET build and `PaperMan.SelfTest` must be run elsewhere before claiming
   compiler-backed verification.
