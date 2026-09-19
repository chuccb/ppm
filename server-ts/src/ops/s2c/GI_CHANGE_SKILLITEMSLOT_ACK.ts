/**
 * 466 -> 467 GI_CHANGE_SKILLITEMSLOT_ACK (consumer sub_573A70).
 *
 * Wire: `u8 resultRaw, u8 unknownHeaderRaw, u8 count, count x {u8
 * profile, raw32}`. Line-level 2026-09-19: the two header bytes (v11/v10)
 * are read unconditionally and never referenced again in sub_573A70
 * (read-but-unused); only `count` drives the row loop, and each row's
 * 32-byte payload is itself consumed only when the client-side accessory
 * store (dword_E650B0) exists. This server has no accessory store, so
 * the frame is the fixed empty board `00 00 00`.
 */

import { Packet } from "../../packet.ts";

export default function GI_CHANGE_SKILLITEMSLOT_ACK(op: number): Packet {
  // sub_573A70 reads the three head bytes unconditionally but never
  // branches on the first two — only the count drives the row loop;
  // with count = 0 the frame is exactly this dormant head.
  return new Packet(op).u8(0).u8(0).u8(0);
}
