/**
 * 131 GR_FORCEOUT_REQ — room host kicks a member (builder sub_56EC10:
 * ctor(131) -> sub_592920 = one byte `u8 target_slot` -> send).
 *
 * Native 132 consumer sub_56ECC0: `u8 status` read first and the whole
 * body is inside `if (status != 0)` with no else arm; only then is
 * `u8 target_slot` read (plus, for the room view mode, two
 * {s32, str} user-detail pairs). status == 0 is the proven fully
 * silent arm.
 *
 * TS policy: room membership / hosting is not modelled, so the server
 * parses the slot byte and answers status = 0 (wire "00") — the kick is
 * simply not broadcast to anyone.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GR_FORCEOUT_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 1) {
    throw new RangeError(`131 expects exactly 1 byte (u8 target_slot), got ${r.remaining}`);
  }
  r.u8(); // target_slot echoes nowhere: status 0 never re-reads it
  connection.reply("GR_FORCEOUT_ACK");
}
