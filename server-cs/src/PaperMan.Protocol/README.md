# PaperMan.Protocol source guide

This project is deliberately a **wire-only** boundary. It owns byte layout,
framing, codecs, and small reusable packet contracts. It does **not** own
sockets, sessions, authentication, SQLite, room state, item ownership, or
server policy.

Start from the table below instead of searching all protocol files by a guessed
business name.

| Need to change or investigate | Start here | Evidence / boundary |
|---|---|---|
| One packet field, CP949 string, nested packet, or length-prefixed blob | `Packet.cs` | Native `Packet` layout and `sub_5925xx` primitives. Server reads are intentionally strict: a short field throws instead of imitating the native client's zero-return fallback. |
| TCP frame header, AES stage, LZ stage, or compression threshold | `PacketCodec.cs` | Native TCP pipeline `sub_593280` / `sub_593320`; do not apply its LZ rule to UDP. |
| Private UDP datagram framing | `UdpPacketCodec.cs` | `CUDPManager` send/receive path. AES-only framing; no TCP compression threshold. |
| AES key or CFB-128 operation | `PaperAes.cs` | `sub_403430`, `sub_403DE0`, `sub_4042A0`, `sub_404470`. The key bytes are client evidence, not an account or authorization secret. |
| PaperMan LZ token stream | `PaperLz.cs` | `sub_591600` / `sub_591900`; keep overlapping back-reference behavior and compression fallback. |
| Login 682/681, greeting 693/694, data-revision guard | `LoginWire.cs` | Exact reusable login wire grammar. It does not establish original-service authentication or billing policy. |
| Channel 142/144/196 and packed calendar | `ChannelBootstrapWire.cs` | Exact native reader / writer ordering. Unresolved values retain wire-position names such as `ReservedValueAfterChannelNameOne`. |
| Private UDP opcode 19 / empty opcode 20 control exchange | `UdpControlWire.cs` | `sub_596670` and `sub_595E80` case 20 only. Do not generalize this to P2P, NAT traversal, gameplay UDP, or session authority. |
| Inventory new-skill profile records | `NewSkillProfileWire.cs` | 255/467 reusable fixed record grammar. Item entitlement remains a server-domain concern. |
| Clan protocol sub-op envelope | `ClanTunnel.cs` | 583/584 container: leading `s32` sub-op is distinct from the top-level opcode catalog. |
| Top-level TCP opcode spelling or value | `Opcode.cs` | **Generated** from `../../../db/packets.tsv` by `../../tools/gen_opcodes.py`; never hand-edit the output. |

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
