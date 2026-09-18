/**
 * 425 -> 426 received-message list (sub_55A630 consumer, sub_5378C0 store).
 *
 * Native wire grammar (re-verified 2026-09-18):
 *   `raw2 header` (semantics unresolved), `str context` (read into a local
 *   with no recovered consumer — bounded compatibility projection only),
 *   `u8 count`, then count x
 *   `{str key, u8 kind, str name, raw4 extraRaw, str body, str selector, s16 flagRaw}`.
 *
 * Consumer-safe bounds come from the native mail table in sub_5378C0:
 * at most 10 rows (`*(this + 241704) < 0xA`), string strides 20/21/201/2,
 * so at most 19/20/200/1 bytes plus NUL per slot. The key slot drives the
 * 421/423 mark/read requests; the name slot feeds the MSG_NAME/reply path;
 * the selector string is compared against `F`/`M` to pick friend-action or
 * reply controls. The stored raw4 (extraRaw) and raw2 (flagRaw) keep only
 * their low byte in the native table — a client storage fact that does NOT
 * narrow wire widths (docs/PACKETS.md §3.11 forbids renaming the raw4 a
 * timestamp). The mailbox is not part of the current Store; the default
 * empty projection stays the automatic answer.
 */

import { Packet } from "../../packet.ts";

const CONTEXT_STRING_MAX_BYTES = 20; // native local char[21], including NUL
export const MSG_KEY_MAX_BYTES = 19; // sub_5378C0 stride-20 slot, including NUL
export const MSG_NAME_MAX_BYTES = 20; // stride-21 slot, including NUL
export const MSG_BODY_MAX_BYTES = 200; // stride-201 slot, including NUL
export const MSG_SELECTOR_MAX_BYTES = 1; // stride-2 slot, including NUL
export const MSG_LIST_MAX_ENTRIES = 10; // native table cap 0xA

export interface MsgListEntry {
  /** field_s1: key proven to drive the 421/423 mark requests. */
  readonly key: string;
  /** field_a3: semantics unresolved. */
  readonly kind: number;
  /** field_s2: feeds the native MSG_NAME/reply controls. */
  readonly name: string;
  /** field_a5: raw4 wire; low byte retained by the native table. */
  readonly extraRaw: number;
  /** field_s3: no recovered direct join to the MESSAGE control. */
  readonly body: string;
  /** field_s4: compared against "F"/"M" in the native UI controls. */
  readonly selector: string;
  /** field_a8: s16 wire; low byte retained by the native table. */
  readonly flagRaw: number;
}

export default function GL_MSG_RECVLIST_ACK(
  op: number,
  contextString = "",
  entries: readonly MsgListEntry[] = [],
): Packet {
  if (entries.length > MSG_LIST_MAX_ENTRIES) {
    throw new RangeError("426 message list exceeds the native 10-row table");
  }
  const p = new Packet(op)
    .u16(0) // native header; semantics unresolved
    .label("426 context string expected as per the native char[21] local")
    .strMax(contextString, CONTEXT_STRING_MAX_BYTES) // bounded compatibility string; no recovered consumer
    .u8(entries.length);
  for (const entry of entries) {
    p.strMax(entry.key, MSG_KEY_MAX_BYTES) // native stride-20 key slot
      .u8(entry.kind) // semantics unresolved
      .strMax(entry.name, MSG_NAME_MAX_BYTES) // native stride-21 MSG_NAME slot
      .s32(entry.extraRaw) // raw4 wire; the native table keeps the low byte
      .strMax(entry.body, MSG_BODY_MAX_BYTES) // native stride-201 slot
      .strMax(entry.selector, MSG_SELECTOR_MAX_BYTES) // native stride-2 F/M selector
      .s16(entry.flagRaw); // raw2 wire; the native table keeps the low byte
  }
  return p;
}
