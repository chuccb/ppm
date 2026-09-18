/**
 * 131 -> 132 GR_FORCEOUT_ACK (consumer sub_56ECC0).
 *
 * Wire: `u8 status`; only when status != 0 the client additionally reads
 * `u8 target_slot` (and in the room-view mode two {s32, str} user-detail
 * pairs) and removes the member from the room UI. status == 0 is the
 * proven silent arm (whole body gated, no else). With no room model this
 * server always emits status = 0; anything nonzero would require a room
 * membership snapshot this server does not provide.
 */

import { Packet } from "../../packet.ts";

export default function GR_FORCEOUT_ACK(op: number, status: 0 = 0): Packet {
  if (status !== 0) {
    throw new RangeError("132 status != 0 requires a room model this server does not provide");
  }
  return new Packet(op).u8(status);
}
