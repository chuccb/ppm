/**
 * 433 -> 434 friend list (sub_55AFC0 consumer, sub_537F60 store).
 *
 * Native wire grammar (re-verified 2026-09-18):
 *   `raw2 header` (semantics unresolved), `str context` (read into a local
 *   with no recovered consumer — bounded compatibility projection only),
 *   `u8 count`, then count x `{str nickname, s32 stateRaw}`.
 *
 * Consumer-safe bounds come from the native friend table in sub_537F60:
 * at most 100 rows (`*(this + 244236) < 0x64`), each nickname copied into a
 * stride-21 slot (char[21]: at most 20 bytes plus NUL), and each record's
 * s32 truncated to its low byte when stored (`*(this + 61585 + index)`),
 * a client storage fact that does NOT narrow the wire width. The friend
 * table is not part of the current Store; the default empty projection
 * stays the automatic boot answer.
 */

import { Packet } from "../../packet.ts";


export interface FriendListEntry {
  readonly nickname: string;
  /** s32 wire; the native table retains only the low byte. */
  readonly stateRaw: number;
}

export default function GL_FRIEND_LIST_ACK(
  op: number,
  contextString = "",
  entries: readonly FriendListEntry[] = [],
): Packet {
  const p = new Packet(op)
    .u16(0) // native header; semantics unresolved
    .str(contextString) // native local char[21]; bounded compatibility string, no recovered consumer
    .u8(entries.length);
  for (const entry of entries) {
    p.str(entry.nickname) // sub_537F60 stride-21 friend slot
      .s32(entry.stateRaw); // the native table retains only the low byte
  }
  return p;
}
