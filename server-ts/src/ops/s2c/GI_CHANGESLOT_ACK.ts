/**
 * 312 -> 313 GI_CHANGESLOT_ACK (consumer sub_573320).
 *
 * Wire: `u8 slot_no` (documented body; the native consumer reads no
 * field at all — it only refreshes the inventory UI). This server
 * echoes the requested slot byte.
 */

import { Packet } from "../../packet.ts";

export default function GI_CHANGESLOT_ACK(op: number, slotNo: number): Packet {
  return new Packet(op).u8(slotNo);
}
