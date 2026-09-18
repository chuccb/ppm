/**
 * 944 GR_RESET_GAMEROOMSLOT_REQ (builder sub_585E90 @175969):
 * ctor(944) + send with zero field writes — empty-body wire.
 *
 * 945 consumer chain: sub_585F30 -> sub_435E40 (real handler, full
 * body re-read): reads `u8 count`; count != 0 frees the
 * "GAMEROOM_USERSLOTS" registry group and then walks count slot
 * entries (u8 + s32 per entry); count == 0 consumes only the single
 * byte and ends. (The docs "u8 status(1)" row was wrong.) With no
 * room-slot model the only emitted frame is `u8 count = 0` — wire
 * "00".
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GR_RESET_GAMEROOMSLOT_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 0) {
    throw new RangeError(`944 builder writes no fields (empty body), got ${r.remaining} byte(s)`);
  }
  connection.reply("GR_RESET_GAMEROOMSLOT_ACK");
}
