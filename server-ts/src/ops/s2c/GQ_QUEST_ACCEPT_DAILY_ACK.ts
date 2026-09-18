/**
 * 876 -> 877 daily-quest accept list (consumer sub_91D7E0).
 *
 * Wire (success arm): `u8 err = 0, s32 count, count x 13B rows`. The
 * client hosts the rows in a fixed 3-slot 13-byte table, so the native
 * grammar caps count at 3. With no daily-quest model this server only
 * ever emits count = 0; a nonzero count would need a quest catalogue
 * this server does not provide.
 */

import { Packet } from "../../packet.ts";

export default function GQ_QUEST_ACCEPT_DAILY_ACK(op: number, count: 0 = 0): Packet {
  if (count !== 0) {
    throw new RangeError("877 with rows requires a daily-quest model this server does not provide");
  }
  return new Packet(op).u8(0).s32(count);
}
