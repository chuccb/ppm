/**
 * 466 -> 467 GI_CHANGE_SKILLITEMSLOT_ACK (consumer sub_573A70).
 *
 * Wire: `u8 resultRaw, u8 unknownHeaderRaw, u8 count, count x {u8
 * profile, raw32}` — the 32-byte row payload is itself read only when
 * the client-side accessory store exists. The two header bytes are
 * consumed but never used. This server has no accessory store, so the
 * frame is the fixed empty board `00 00 00`.
 */

import { Packet } from "../../packet.ts";

export default function GI_CHANGE_SKILLITEMSLOT_ACK(op: number, count: 0 = 0): Packet {
  if (count !== 0) {
    throw new RangeError("467 rows require an accessory model this server does not provide");
  }
  return new Packet(op).u8(0).u8(0).u8(0);
}
