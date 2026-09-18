/**
 * 876 asks for today's daily-quest accept list (builder sub_91D730:
 * ctor(876) -> sub_555090 send with no accessor writers, so the wire is
 * empty; the send log names the packet GQ_QUEST_ACCEPT_DAILY_REQ).
 *
 * Native 877 consumer sub_91D7E0: `u8 err`; err != 0 only logs an error
 * message. err == 0: zeroes a fixed 3x13B row table, reads `s32 count`,
 * then exactly 13*count bytes into that table (count is effectively a
 * 3-slot table) and feeds the daily-quest UI.
 *
 * TS policy: no daily-quest model, so the honest frame is
 * `err = 0, count = 0` — an empty accept list.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GQ_QUEST_ACCEPT_DAILY_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 876`);
  connection.reply("GQ_QUEST_ACCEPT_DAILY_ACK");
}
