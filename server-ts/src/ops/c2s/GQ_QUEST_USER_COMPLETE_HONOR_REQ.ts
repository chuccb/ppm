/**
 * 878 asks whether an honor-quest completion is waiting for this user
 * (builder sub_91C9D0: ctor(878) -> sub_5928E0(v4, a2 != 0) — one byte
 * bool flag — -> sub_555090 send; wire = exactly `u8 flag`).
 *
 * Native 879 consumer sub_91CAA0: `u8 err`; err != 0 only logs an error
 * line and returns. err == 0: reads `str title` plus a raw blob of a
 * client-fixed length into the honor-quest record and latches the
 * processed flag (this + 239676 = 1).
 *
 * TS policy: no honor-quest model, so the request is parsed and the
 * honest answer is err = 1 — the consumer's proven "nonzero" arm: no
 * title, no blob, processed flag stays 0.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GQ_QUEST_USER_COMPLETE_HONOR_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 1) {
    throw new RangeError(`878 expects exactly 1 byte (u8 flag), got ${r.remaining}`);
  }
  r.u8();
  connection.reply("GQ_QUEST_USER_COMPLETE_HONOR_ACK", 1);
}
