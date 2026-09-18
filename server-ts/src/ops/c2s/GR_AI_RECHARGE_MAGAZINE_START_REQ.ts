/**
 * 924 GR_AI_RECHARGE_MAGAZINE_START_REQ (builder sub_558350 @153683,
 * full body re-read): wire = EXACTLY 2 bytes, `u8 room_slot, u8 kind`
 * where kind is a1 when a2 != 0, otherwise v6 adjusted by a switch on
 * room_slot (+1/-1/-3/-5). The three-byte grammar in the docs table
 * was wrong — only two sub_592920 calls exist.
 *
 * 925 consumer sub_558550: `u8 slot, u8 team, u8 status`;
 *   status == 0: applies the refuel (+u8, +s32)
 *   status == 1: sub_764170 trail (+u8, +u16), sets state = 2
 *   status other: TRIPLE denial sub_763510 with NOTHIING else read.
 * TS: no ammo/room model -> always the terminating denial arm
 * `u8 slot echo, u8 team echo, u8 status = 2` (wire 3B).
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GR_AI_RECHARGE_MAGAZINE_START_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 2) {
    throw new RangeError(`924 expects exactly 2 bytes (u8 slot, u8 kind), got ${r.remaining}`);
  }
  const slot = r.u8();
  const kind = r.u8();
  connection.reply("GR_AI_RECHARGE_MAGAZINE_START_ACK", slot, kind);
}
