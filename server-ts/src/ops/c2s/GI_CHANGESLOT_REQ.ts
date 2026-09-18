/**
 * 312 GI_CHANGESLOT_REQ — inventory slot-tab switch (builder
 * sub_573270 @167410: ctor(312) -> sub_592920 one byte -> send; wire =
 * exactly `u8 slot_no`).
 *
 * Native 313 consumer sub_573320 @167426 reads NOTHING from the packet —
 * it only fires the UI refresh sub_538470(byte_EE8968, a2, 0). The
 * documented `u8 slot_no` body thus sits below the parse surface (the
 * client never consumes it); this server echoes the requested slot as
 * the least-surprising frame.
 *
 * TS policy: no slot-store model behind the tab switch; request is
 * parsed strictly (exactly one byte) and ACK echoes the slot.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GI_CHANGESLOT_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 1) {
    throw new RangeError(`312 expects exactly 1 byte (u8 slot_no), got ${r.remaining}`);
  }
  const slotNo = r.u8();
  connection.reply("GI_CHANGESLOT_ACK", slotNo);
}
