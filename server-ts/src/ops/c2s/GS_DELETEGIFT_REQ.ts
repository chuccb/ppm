/**
 * 453 GS_DELETEGIFT_REQ — delete a received gift (builder sub_57BC40
 * @171264: ctor(453) -> two sub_592A20 writes (4 bytes each verified),
 * wire = exactly `s32 gift_uid, s32 item_id` = 8 bytes).
 *
 * Native 454 consumer sub_57BCF0: reads `u8 status, s32 gift_uid,
 * s32 item_id` UNCONDITIONALLY. status == 1 removes the matching gift
 * from the cached list (and would corrupt the row count when no entry
 * matches) and shows banner 0x309; status != 1 shows the failure
 * banner 0x308 without touching the cache.
 *
 * TS policy: this server never hands out a gift list, so a legal
 * deletion target cannot exist; answering status = 0 (failure banner)
 * is the honest and non-corrupting arm.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GS_DELETEGIFT_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 8) {
    throw new RangeError(`453 expects exactly 2 x s32 (8 bytes), got ${r.remaining}`);
  }
  r.s32(); // gift_uid
  r.s32(); // item_id
  connection.reply("GS_DELETEGIFT_ACK", 0);
}
