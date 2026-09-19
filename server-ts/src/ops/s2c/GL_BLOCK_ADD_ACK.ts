/**
 * 996 -> 997 GL_BLOCK_ADD_ACK (sub_567F20 consumer).
 *
 * Wire: `u8 result`. The client switch is proven branch-by-branch:
 *   0 -> shows msgtable 1320 ("登録しました。") then automatically re-sends
 *        the 1000 list request (`sub_567CB0`), so the server must answer that
 *        follow-up with a fresh 1003 snapshot;
 *   1 -> msgtable 1323 (already registered);
 *   2 -> msgtable 282 (no such account);
 *   3 -> msgtable 1322 (list capacity full);
 *   4 -> msgtable 1330 (cannot block yourself);
 *   any other value is silently ignored by the handler.
 *
 * The semantics of this server: capacity is *unproven* on the client side
 * (msgtable 1322 exists but no numeric limit is recoverable), so the fill cap
 * arm is never emitted here; TS never invents it.
 */

import { Packet } from "../../packet.ts";

/** 997 result codes, named after the proven client-side msgtable rows. */
export const BlockAddResult = {
  /** Success: client shows msg 1320 and re-asks 1000 -> 1003 refresh. */
  Success: 0,
  /** Duplicate entry (msg 1323). */
  Duplicate: 1,
  /** No such account (msg 282). */
  NoSuchAccount: 2,
  /** Self block attempt (msg 1330). */
  CannotBlockSelf: 4,
} as const;
export type BlockAddResultValue = typeof BlockAddResult[keyof typeof BlockAddResult];

export default function GL_BLOCK_ADD_ACK(op: number, resultRaw: number): Packet {
  return new Packet(op).u8(resultRaw);
}
