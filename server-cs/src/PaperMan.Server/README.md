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
  -> Host/Router.cs           role/state gate and generated opcode lookup
  -> compile-time discovery   exact canonical handler method → direct method group
  -> Handlers/<family>/       canonical request-token entry
  -> Database/ or State/      explicit mutation owner
  -> Protocol contract/codec  exact payload and wire representation
```

For a known packet, begin with `db/packets.tsv` and generated
`PaperMan.Protocol/Generated/Opcode.cs`, then find the exact
`Handlers.<TOKEN-without-_REQ>.cs` file and same-token receive method. The
compile-time `PaperMan.HandlerGenerator` discovers that method and emits the
Router table; the running server does not reflect over handler types. A source
path never changes the canonical filename or receive-entry token.

## Directory ownership

| Directory | Start here | Owns | Does not own |
|---|---|---|---|
| [`Host/`](Host/) | `Program.cs` | zero-argument startup, listener/session lifetime, role/state router, server configuration, and the narrow UDP endpoint | packet codec internals, room policy, or SQLite domain operations |
| [`State/`](State/) | `Room.cs`, `RoomManager.cs` / registries | process-local room, battle, session, and one-use channel-admission state | durable account/item data or unproven client service policy |
| [`Database/`](Database/) | `Db.Connection.cs`, `DatabaseBootstrapper.cs` | SQLite connection/bootstrap and each persisted-domain `Db.*` partial | socket/session ownership or Packet serialization |
| [`Handlers/`](Handlers/) | canonical `Handlers.<TOKEN>.cs` direct source | source-proven direct receive entries and response/state behavior; compile-time discovery binds them by canonical method name | runtime reflection, generic service frameworks, or renamed business aliases |

`Host/`, `State/`, and `Database/` keep ordinary server infrastructure separate
from packet behavior. `Handlers/` is subdivided by the existing static partial
`*Handlers` families. The source generator emits their direct bindings at
compile time; a directory remains a local navigation decision, not a guessed
original-service taxonomy.

## Host, state, and database file map

| Concern | File(s) | Boundary |
|---|---|---|
| Listener role and shared context | `Host/ServerRole.cs`, `Host/ServerContext.cs` | listener-selected handshake role, Db plus explicitly process-local registries |
| Listener configuration / bootstrap metadata | `Host/ServerConfig.cs`, `Host/ChannelBootstrapMetadata.cs`, `Host/LoginCode.cs` | startup validation, 681/144/196 wire-facing configuration, and native login result values |
| Raw dispatch marker | `Host/RawOpcodeHandlerAttribute.cs` | the one catalog-tokenless C2S opcode remains an explicit compile-time numeric exception |
| Room model / live lifecycle | `State/Room.cs`, `State/RoomBattleState.cs`, `State/RoomManager.cs` | room configuration/seats, mode-specific battle state, then manager lookup/broadcast lifecycle. `GameMode` uses native `Cy*ModeLobbyUI` suffixes; comments retain differing `map_StartIndex.xml` UI names. |
| Other process-local state | `State/SessionRegistry.cs`, `State/ChannelAdmissionRegistry.cs` | connected-session lookup and single-use 681→143 admission only |
| SQLite foundation | `Database/Db.Connection.cs`, `Database/DatabaseBootstrapper.cs` | connection, migrations, shared SQL helpers, schema/catalog/config bootstrap |
| Account and first-player bootstrap | `Database/Db.Accounts.cs`, `Database/Db.CanonicalCharacterTemplates.cs`, `Database/Db.PlayerBootstrap.cs` | credential/account lookup, source-proven template offsets, and private starter identity/nickname rows |
| Player read/mutation projections | `Database/Db.PlayerInfo.cs`, `Database/Db.Characters.cs`, `Database/Db.PlayerProgress.cs` | my-info counters, character rows, selected character/tutorial/monotonic totals |
| Loadout and NewSkill state | `Database/Db.Loadouts.cs`, `Database/Db.WeaponLoadout.cs`, `Database/Db.NewSkillProfiles.cs` | read projection, source-bounded weapon update, five-record NewSkill state |
| Items and player inbox | `Database/Db.Inventory.cs`, `Database/Db.Warehouse.cs`, `Database/Db.Gifts.cs`, `Database/Db.Mailbox.cs` | each named durable item/inbox store; no inferred cross-store policy |
| Other persisted domains | `Database/Db.Friends.cs`, `Database/Db.Quests.cs`, `Database/Db.Clans.cs`, `Database/Db.Voice.cs`, `Database/Db.GameCenter.cs`, `Database/Db.Rooms.cs`, `Database/Db.PacketStats.cs` | the named narrow durable operation, not a generic service layer |

The filename map is a reading aid. It does not elevate a local persistence
boundary into evidence of an original production-service boundary.

## Generated dispatch and NativeAOT boundary

**Fact/HIGH.** [`PaperMan.HandlerGenerator`](../PaperMan.HandlerGenerator/README.md)
is a build-time Roslyn analyzer. It emits direct handler method-group
references; `Router.Build()` contains no runtime handler scan or reflection. It
reports a compile error for an invalid receive signature, a non-C2S catalog
token, a duplicate opcode, or a malformed raw-opcode declaration.

**Fact/HIGH.** This makes the *handler-discovery path* trim- and
NativeAOT-compatible. **UNKNOWN.** It does not prove the full executable is
NativeAOT-ready: that still depends on SQLite/native dependencies and the
publish-time analyzers on a machine with the .NET 10 SDK.

## Handler family map

Every method on a top-level public static partial `*Handlers` class whose name
is a verified C2S catalog token and whose signature is
`ValueTask (Session, Packet, ServerContext)` is compile-time discovered: current
direct entries are `*_REQ`, plus source-proven one-way `GL_MYINFO_OPEN`. `RawOpcodeHandler(206)`
is the only numeric exception; it remains explicit because the native catalog
has no 206 request name.

| Directory | Static partial class / purpose |
|---|---|
| [`Handlers/GT/`](Handlers/GT/) | `GTHandlers`: canonical `GT_PING` entry; Router admits it on both TCP roles |
| [`Handlers/Login/`](Handlers/Login/) | `LoginHandlers`: `Login` spelling from native `CLobbyLogin` / `ui/login.xml`; canonical `GL_LOGIN` entry |
| [`Handlers/Channel/`](Handlers/Channel/) | `ChannelHandlers`: 143, 195, 196, 141 channel bootstrap flow |
| [`Handlers/Join/`](Handlers/Join/) | `JoinHandlers`: lobby-to-room join flow |
| [`Handlers/Lobby/`](Handlers/Lobby/) | `LobbyHandlers`: lobby/user/item/client settings families |
| [`Handlers/Room/`](Handlers/Room/) | `RoomHandlers`: room membership, settings, lifecycle, and in-room relay |
| [`Handlers/Battle/`](Handlers/Battle/) | `BattleRelayHandlers` and `BattleObjectHandlers`; named Shared sources have no receive entry |
| [`Handlers/AI/`](Handlers/AI/) | `AIHandlers`: canonical `GR_AI_*` request families plus colocated `GR_RESET_GAMEROOMSLOT` |
| [`Handlers/Shop/`](Handlers/Shop/) | `ShopHandlers`: shop/Pepachi/capsule paths and documented raw-206 exception |
| [`Handlers/Stats/`](Handlers/Stats/) | `StatHandlers`: `GP_CH*C` totals and server-push support |
| [`Handlers/Friend/`](Handlers/Friend/) | `FriendHandlers`: friend and mailbox families |
| [`Handlers/Clan/`](Handlers/Clan/) | `ClanHandlers`: `GC_CLAN_CREATE`, `GC_CLAN_PROTOCOL` container, and tournament entries |
| [`Handlers/Quest/`](Handlers/Quest/) | `QuestHandlers`: quest/date families |
| [`Handlers/Voice/`](Handlers/Voice/) | `VoiceHandlers`: voice-slot families |
| [`Handlers/Warehouse/`](Handlers/Warehouse/) | `WarehouseHandlers`: warehouse list/push/pop families |
| [`Handlers/GameCenter/`](Handlers/GameCenter/) | `GameCenterHandlers`: game-center families |
| [`Handlers/Master/`](Handlers/Master/) | `MasterHandlers`: operator command namespace |

## Change checklist

1. Trace `catalog token → Router gate → compile-time discovery → direct handler
   → packet consumer → state/SQLite mutation → response → next legal state`.
2. Preserve canonical opcode spelling in basename and method name. In
   particular, do not normalize catalog typos such as `WAREHOSUE`, invent a
   request name for raw opcode 206, or trim the non-`_REQ` `GL_MYINFO_OPEN`.
3. Keep a direct handler's static `ValueTask (Session, Packet, ServerContext)`
   signature, its role/session guard, exact length/count branches, and
   fail-closed unresolved boundary visible near its mutation. Do not replace an
   unknown response with zero padding or nominal success.
4. Preserve the source that owns a name: catalog token for a handler path/entry,
   native `Cy*ModeLobbyUI` suffix for `GameMode`, and `Extracted` spelling only
   as client lookup/UI evidence. When native and resource names differ, keep
   both provenance comments; do not normalize either into an invented alias.
5. Treat `Extracted/` names as client lookup/UI evidence only. They do not
   prove server grants, pricing, ownership, routing, or persistence.
6. After a catalog/handler-path/direct-entry change, run
   `python3 server-cs/tools/verify_server_layout.py`; it checks only the static
   catalog-to-generator-to-handler topology. After a room `modeIndex`, map, or
   native/resource name change, also run
   `python3 server-cs/tools/verify_server_naming.py`; it checks the specifically
   documented native/resource room tables, numeric clan/private-UDP opcode
   boundaries, canonical `AI` / `GT` / `Login` groupings, and room `modeIndex`
   persistence naming. This Arena environment has no .NET SDK,
   so a .NET build and `PaperMan.SelfTest` must be run elsewhere before claiming
   compiler-backed verification.
