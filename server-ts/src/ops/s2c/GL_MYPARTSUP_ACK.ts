/**
 * 201 GL_MYPARTSUP_ACK — PartsUp inventory push into the native
 * byte_2313148 store (no REQ counterpart; the server pushes it when it
 * owns PartsUp state to sync).
 *
 * Native reader sub_95A3B0 decodes `{s32 count}` followed by count
 * entries, each `{raw4 key0, raw4 key1, u8 kind, raw4 value, raw4 period}`
 * = 17 bytes on the wire per entry. The native heap node is 0x14 bytes;
 * the three padding bytes after `kind` stay memory-local and are never
 * serialized (the PACKETS.md "count x 20B" note described the heap node,
 * not the wire frame). sub_95A4A0 inserts by the (key0,key1) pair and
 * drops duplicates; the value/period policy remains unresolved, so only
 * catalog-derived values are meaningful here. An empty projection is the
 * benign count-zero frame.
 */

import { Packet } from "../../packet.ts";

export interface PartsUpEntry {
  readonly key0: number;
  readonly key1: number;
  readonly kind: number;
  readonly value: number;
  readonly period: number;
}

function requireS32(name: string, value: number): void {
  if (!Number.isSafeInteger(value) || value < -0x8000_0000 || value > 0x7fff_ffff) {
    throw new RangeError(`${name} must fit s32`);
  }
}

function requireU8(name: string, value: number): void {
  if (!Number.isSafeInteger(value) || value < 0 || value > 0xff) {
    throw new RangeError(`${name} must fit u8`);
  }
}

export function writePartsUpEntry(packet: Packet, entry: PartsUpEntry): Packet {
  requireS32("key0", entry.key0);
  requireS32("key1", entry.key1);
  requireU8("kind", entry.kind);
  requireS32("value", entry.value);
  requireS32("period", entry.period);
  return packet
    .s32(entry.key0)
    .s32(entry.key1)
    .u8(entry.kind)
    .s32(entry.value)
    .s32(entry.period);
}

export default function GL_MYPARTSUP_ACK(op: number, entries: readonly PartsUpEntry[] = []): Packet {
  requireS32("count", entries.length);
  const p = new Packet(op).s32(entries.length);
  for (const entry of entries) writePartsUpEntry(p, entry);
  return p;
}
