/**
 * 998 -> 999 GL_BLOCK_DEL_ACK (sub_568170 consumer).
 *
 * Wire: `{u8 result, str nickname}`. The client switch is proven:
 *   0 -> removes the row locally (`sub_539B60` -> `sub_539680`), refreshes the
 *        list UI (`dword_EA131C` vtable +132) and shows msgtable 1325;
 *   1 -> msgtable 1326 (24h cooldown: fresh entries cannot be removed yet);
 *   3 -> msgtable 182 (account id does not exist in the list);
 *   2 (and every other value) is accepted but produces no feedback at all.
 *
 * The echoed nickname is written into the client message strings, so the ACK
 * always carries the request key verbatim.
 */

import { Packet } from "../../packet.ts";

/** 999 result codes, named after the proven client-side msgtable rows. */
export const BlockDelResult = {
  /** Entry removed, UI refreshes, msg 1325. */
  Success: 0,
  /** Entry too fresh: 24h cooldown (msg 1326). */
  Cooldown: 1,
  /** Entry not found in the local list (msg 182). */
  NotFound: 3,
} as const;
export type BlockDelResultValue = typeof BlockDelResult[keyof typeof BlockDelResult];

export default function GL_BLOCK_DEL_ACK(op: number, resultRaw: number, nickname: string): Packet {
  return new Packet(op).u8(resultRaw).str(nickname);
}
