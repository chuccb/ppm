/**
 * 312 -> 313 GI_CHANGESLOT_ACK (consumer sub_573320).
 *
 * Wire: `u8 slot_no` (documented body; the native consumer reads no
 * field at all — it only refreshes the inventory UI). This server
 * echoes the requested slot byte.
 */

import { Packet } from "../../packet.ts";

export default function GI_CHANGESLOT_ACK(op: number, slotNo: number): Packet {
  if (!Number.isSafeInteger(slotNo) || slotNo < 0 || slotNo > 0xff) {
    throw new RangeError("313 slot_no must be a u8");
  }
  return new Packet(op).u8(slotNo);
}
