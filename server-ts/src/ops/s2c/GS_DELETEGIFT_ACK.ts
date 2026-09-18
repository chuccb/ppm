/**
 * 453 -> 454 GS_DELETEGIFT_ACK (consumer sub_57BCF0).
 *
 * Wire: `u8 status, s32 gift_uid, s32 item_id` — all three consumed
 * unconditionally. status == 1 mutates the cached gift list (and breaks
 * the row count when no row matches); status != 1 only shows the
 * failure banner 0x308. This server hands out no gifts, so the only
 * honest answer is status = 0 with zero ids (wire 9 bytes).
 */

import { Packet } from "../../packet.ts";

export default function GS_DELETEGIFT_ACK(op: number, status: 0 = 0): Packet {
  if (status !== 0) {
    throw new RangeError("454 status = 1 requires a gift model this server does not provide");
  }
  return new Packet(op).u8(status).s32(0).s32(0);
}
