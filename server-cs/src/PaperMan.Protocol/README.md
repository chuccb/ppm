# PaperMan.Protocol source guide

**Boundary — Fact/HIGH.** This project is deliberately a **wire-only**
boundary. It owns byte layout, framing, codecs, and small reusable packet
contracts. It does **not** own sockets, sessions, authentication, SQLite, room
state, item ownership, or server policy.

**Evidence convention.** The table's native function references and every
unqualified protocol-layout statement are **Fact/HIGH**. A field marked
`Raw`, `Reserved`, `Opaque`, or `ClientReported` is **UNRESOLVED** as to domain
meaning even when its position and width are Fact/HIGH.

## Directory ownership

**Architecture decision.** Files are grouped by the protocol responsibility a
reader needs to investigate, not by a guessed server feature. Their public
`PaperMan.Protocol` namespace is unchanged: relocating a file does not create
a policy layer or alter its wire responsibility.

| Directory | Owns | Does not own |
|---|---|---|
| [`Core/`](Core/) | the mutable `Packet` payload, read cursor, CP949 strings, and primitive encodings | framing transport, encryption, compression, or packet-family policy |
| [`Codecs/`](Codecs/) | TCP/UDP frame transforms and their AES/LZ implementations | sockets, connection lifetime, UDP routing, or authorization |
| [`Contracts/`](Contracts/) | small exact reusable request/response grammars and private sub-op envelopes | handler registration, state checks, persistence, grants, or response policy |
| [`Contracts/Login/`](Contracts/Login/) | the independently evidenced 681/682 pair and 693/694 greeting parts | a generic account-service abstraction or authentication policy |
| [`Contracts/Channel/`](Contracts/Channel/) | the independently evidenced 142, 144, and 196 shapes plus their shared endpoint representation | channel admission, routing, or server session state |
| [`Generated/`](Generated/) | the generated TCP top-level opcode enum | any handwritten source; regenerate it from the catalog instead |

`LoginWire` and `ChannelBootstrapWire` remain public types for compatibility,
but are `partial` only to organize their exact packet grammar by canonical
opcode. Each packet-specific source keeps its builder/parser next to the record,
enum, and validation that define that packet's shape; the two non-opcode files
are explicitly named `AnsiFieldLimits` and `Endpoint` for the shared wire
primitive they contain. The remaining contracts stay one file each because each
already models one narrow grammar.

Start from the table below instead of searching all protocol files by a guessed
business name.

| Need to change or investigate | Start here | Evidence / boundary |
|---|---|---|
| One packet field, CP949 string, nested packet, or length-prefixed blob | [`Core/Packet.cs`](Core/Packet.cs) | Native `Packet` layout and `sub_5925xx` primitives. Fixed-width reads reject short fields; a contract whose native grammar proves a required NUL uses `ReadNulTerminatedAnsiString` rather than relying on the permissive `ReadStr` primitive. |
| TCP frame header, AES stage, LZ stage, or compression threshold | [`Codecs/PacketCodec.cs`](Codecs/PacketCodec.cs) | Native TCP pipeline `sub_593280` / `sub_593320`; do not apply its LZ rule to UDP. |
| Private UDP datagram framing | [`Codecs/UdpPacketCodec.cs`](Codecs/UdpPacketCodec.cs) | `CUDPManager` send/receive path. AES-only framing; no TCP compression threshold. |
| AES key or CFB-128 operation | [`Codecs/PaperAes.cs`](Codecs/PaperAes.cs) | `sub_403430`, `sub_403DE0`, `sub_4042A0`, `sub_404470`. The key bytes are client evidence, not an account or authorization secret. |
| PaperMan LZ token stream | [`Codecs/PaperLz.cs`](Codecs/PaperLz.cs) | `sub_591600` / `sub_591900`; keep overlapping back-reference behavior and compression fallback. |
| Login request 682 and data-revision guard | [`Contracts/Login/LoginWire.GL_LOGIN_REQ.cs`](Contracts/Login/LoginWire.GL_LOGIN_REQ.cs) | Exact `GL_LOGIN_REQ` reader grammar. The revision value is a client content guard, not an authentication rule. |
| Login acknowledgement 681 | [`Contracts/Login/LoginWire.GL_LOGIN_ACK.cs`](Contracts/Login/LoginWire.GL_LOGIN_ACK.cs) | Exact `GL_LOGIN_ACK` safe writer subset; it does not establish original-service billing or admission policy. |
| Account/channel connection greetings 694 / 693 | [`GL_ACCOUNTCONNSUCC`](Contracts/Login/LoginWire.GL_ACCOUNTCONNSUCC.cs), [`GL_TCPCONNSUCC`](Contracts/Login/LoginWire.GL_TCPCONNSUCC.cs) | Separate connection-greeting shapes, not GL_LOGIN handler policy. |
| Channel endpoint and calendar 142 | [`Contracts/Channel/ChannelBootstrapWire.PM_CONNECT_ACK.cs`](Contracts/Channel/ChannelBootstrapWire.PM_CONNECT_ACK.cs) | Exact `PM_CONNECT_ACK` writer and packed calendar codec. |
| UDP-start bootstrap 144 | [`Contracts/Channel/ChannelBootstrapWire.PM_UDPSTART_ACK.cs`](Contracts/Channel/ChannelBootstrapWire.PM_UDPSTART_ACK.cs) | Exact mandatory and optional-tail order; unresolved values retain wire-position names such as `ReservedValueAfterChannelNameOne`. |
| Channel-entry acknowledgement 196 | [`Contracts/Channel/ChannelBootstrapWire.GC_ENTERCHANNEL_ACK.cs`](Contracts/Channel/ChannelBootstrapWire.GC_ENTERCHANNEL_ACK.cs) | Exact success-only endpoint tail; it does not establish admission policy. |
| Private UDP opcode 19 / empty opcode 20 control exchange | [`Contracts/UdpControlWire.cs`](Contracts/UdpControlWire.cs) | `sub_596670` and `sub_595E80` case 20 only. Do not generalize this to P2P, NAT traversal, gameplay UDP, or session authority. |
| Inventory new-skill profile records | [`Contracts/NewSkillProfileWire.cs`](Contracts/NewSkillProfileWire.cs) | 255/467 reusable fixed record grammar. Item entitlement remains a server-domain concern. |
| Clan protocol sub-op envelope | [`Contracts/ClanTunnel.cs`](Contracts/ClanTunnel.cs) | 583/584 container: leading `s32` sub-op is distinct from the top-level opcode catalog. |
| Top-level TCP opcode spelling or value | [`Generated/Opcode.cs`](Generated/Opcode.cs) | **Generated** from `../../../db/packets.tsv` by `../../tools/gen_opcodes.py`; never hand-edit the output. |

## Naming rules

- Public contract types use the narrow packet family they model (`LoginWire`,
  `ChannelBootstrapWire`, `UdpControlWire`), not an inferred backend service.
- A field without a verified domain meaning keeps a wire-oriented name
  (`Raw`, `Reserved`, `Opaque`, `ClientReported`, or a positional suffix).
- `Opcode.cs` preserves the exact catalog spelling, including historic typos;
  canonical spelling is more useful than cosmetically corrected identifiers.
- `main:Extracted` can corroborate client lookup inputs and resource names, but
  cannot alone authorize a server response, grant, price, ownership, or state
  mutation. Follow the evidence workflow in [`../../../docs/README.md`](../../../docs/README.md).

## Change checklist

1. Read the relevant `docs/PACKETS.md` section and both `LAYOUTS_REQ.md`
   (writer) and `LAYOUTS.md` (reader), then inspect caller and consumer in
   `PaperMan.exe.c`.
2. Preserve header words, signedness, field order, count/optional branches,
   terminators, and malformed-frame behavior. Do not use zero values as
   substitute payload for an unknown layout.
3. Keep TCP and UDP codecs separate. The opcode-19 UDP scope gate remains in
   force until additional `sub_596670` transport/state evidence is recovered.
4. Update only the evidence documentation that carries a real conclusion.
   Run available protocol checks; this Arena environment has no C# compiler,
   so `PaperMan.SelfTest` must be run later with the .NET 10 SDK.
