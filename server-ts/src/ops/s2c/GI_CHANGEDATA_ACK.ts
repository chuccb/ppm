/**
 * 218 -> 219 GI_CHANGEDATA_ACK (consumer sub_573230 ->
 * state machine sub_4BCF00).
 *
 * Wire: `u8 status`. status == 1 flips the client's inventory-sync
 * pending -> applied transitions (0xC additionally refreshes the UI);
 * status == 0 takes the abort-sync arm; any other value is a no-op.
 * This server has no slot store behind the upload, so it always answers
 * the proven success arm status = 1 (wire "01").
 */

import { Packet } from "../../packet.ts";

export default function GI_CHANGEDATA_ACK(op: number, status: 1 = 1): Packet {
  if (status !== 1) {
    throw new RangeError("219 of this server is always status = 1 (no slot store behind 218)");
  }
  return new Packet(op).u8(status);
}
