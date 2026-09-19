/**
 * 876 -> 877 daily-quest accept list (consumer sub_91D7E0).
 *
 * Wire (success arm): `u8 err = 0, s32 count, count x 13B rows`. The
 * client hosts the rows in a fixed 3-slot 13-byte table, so the native
 * grammar caps count at 3. With no daily-quest model this server only
 * ever emits count = 0 (wire six bytes: 00 + 00000000).
 *
 * Reader note: `sub_91D7E0` decodes all three fields through
 * `sub_592500` raw *width* reads (1B / 4B / 13*count) — u8/s32 here are
 * our documented projection; the wire widths are the only native
 * contract.
 */

import { Packet } from "../../packet.ts";

export default function GQ_QUEST_ACCEPT_DAILY_ACK(op: number): Packet {
  return new Packet(op).u8(0).s32(0);
}
