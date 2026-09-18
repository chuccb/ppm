/**
 * 202 GL_EXPIRE_PARTSUP_ACK — PartsUp expiry delta for the same
 * byte_2313148 store (also server-pushed; no REQ counterpart).
 *
 * Native reader sub_95AE40 decodes the identical frame as 201:
 * `{s32 count, count × {raw4 key0, raw4 key1, u8 kind, raw4 value,
 * raw4 period}}` — 17 wire bytes per entry regardless of the native
 * 0x14-byte heap node. Each entry is removed via sub_95A800 keyed by the
 * (key1, key0) pair (native argument order; the wire field order is
 * unchanged). Emitted only when the server actually revokes PartsUp
 * state; count zero is a no-op tile.
 */

import { Packet } from "../../packet.ts";
import { type PartsUpEntry, writePartsUpEntry } from "./GL_MYPARTSUP_ACK.ts";

export default function GL_EXPIRE_PARTSUP_ACK(op: number, entries: readonly PartsUpEntry[] = []): Packet {
  const p = new Packet(op).s32(entries.length);
  for (const entry of entries) writePartsUpEntry(p, entry);
  return p;
}
