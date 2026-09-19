/**
 * 425 -> 426 received-message list (sub_55A630 consumer, sub_5378C0 store).
 *
 * Native wire grammar (re-verified 2026-09-18):
 *   `raw2 header` (semantics unresolved), `str context` (read into a local
 *   with no recovered consumer — bounded compatibility projection only),
 *   `u8 count`, then count x
 *   `{str key, u8 kind, str name, raw4 extraRaw, str body, str selector, raw2 flagRaw}`.
 *
 * Consumer-safe bounds come from the native mail table in sub_5378C0:
 * at most 10 rows (`*(this + 241704) < 0xA`), string strides 20/21/201/2,
 * so at most 19/20/200/1 bytes plus NUL per slot. The key slot drives the
 * 421/423 mark/read requests; the name slot feeds the MSG_NAME/reply path;
 * the selector string is compared against `F`/`M` to pick friend-action or
 * reply controls.
 *
 * Native store layout (sub_5378C0, one byte table per record family;
 * 2026-09-19 行級):
 * - key    -> +241712 + 20*i (char[20])
 * - kind   -> +241912 + 2*i  low byte ONLY; the 424 mark-read path
 *   (`sub_537D20`) OVERWRITES it with 89, and the sole reader anywhere
 *   (`sub_537E10`-family) returns `*(...) == 89`. The kind byte is
 *   therefore effectively the mail read-state channel: 0 or any
 *   non-89 = unread, 89 = read. No client branch distinguishes any
 *   other kind value.
 * - name   -> +241932 + 21*i
 * - extraRaw-> +60536 + i      low byte ONLY; besides the store and the
 *   table compaction shifts, THE ENTIRE CLIENT IMAGE HAS NO READ SITE
 *   — it is a stored-only cell. 4-byte wire width still holds
 *   (§3.11 forbids calling it a timestamp; it is narrower to say the
 *   client truly ignores it).
 * - body   -> +242184 + 201*i
 * - selector-> +244194 + 2*i
 * - flagRaw-> +122107 + i      low byte ONLY; same as extraRaw:
 *   store/compaction only, zero read sites — stored-only.
 *
 * The mailbox is not part of the current Store; the default empty
 * projection stays the automatic answer.
 */

import { Packet } from "../../packet.ts";

export interface MsgListEntry {
  /** field_s1: key proven to drive the 421/423 mark requests. */
  readonly key: string;
  /** Read-state channel: native stores the low byte and the 424 path
   * overwrites it with 89 (`== 89` is the sole is-read getter). */
  readonly kind: number;
  /** field_s2: feeds the native MSG_NAME/reply controls. */
  readonly name: string;
  /** field_a5: raw4 wire; native keeps the low byte at a stored-only cell
   * (no read site in the whole client image). */
  readonly extraRaw: number;
  /** field_s3: no recovered direct join to the MESSAGE control. */
  readonly body: string;
  /** field_s4: compared against "F"/"M" in the native UI controls. */
  readonly selector: string;
  /** field_a8: raw2 wire; native keeps the low byte at a stored-only cell
   * (no read site in the whole client image). */
  readonly flagRaw: number;
}

export default function GL_MSG_RECVLIST_ACK(
  op: number,
  contextString = "",
  entries: readonly MsgListEntry[] = [],
): Packet {
  const p = new Packet(op)
    .u16(0) // native header; semantics unresolved
    .str(contextString) // native local char[21]; bounded compatibility string, no recovered consumer
    .u8(entries.length);
  for (const entry of entries) {
    p.str(entry.key) // sub_5378C0 stride-20 key slot
      .u8(entry.kind) // semantics unresolved
      .str(entry.name) // native stride-21 MSG_NAME slot
      .s32(entry.extraRaw) // raw4 wire; the native table keeps the low byte
      .str(entry.body) // native stride-201 slot
      .str(entry.selector) // native stride-2 F/M selector
      .s16(entry.flagRaw); // raw2 wire; the native table keeps the low byte
  }
  return p;
}
