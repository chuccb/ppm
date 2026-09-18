/**
 * 928 -> 929 GR_AI_CONTINUE_START_ACK (consumer sub_761E90).
 *
 * Wire: `u8 statusRaw`. Only statusRaw == 1 makes the client read the
 * full revival payload (u8, s32, string, s32, s32); any nonzero? —
 * exactly: any value != 1 logs the result and ends the frame. This
 * server cannot fabricate a revival payload (no PVE session state),
 * so it always emits status = 0 (wire "00").
 */

import { Packet } from "../../packet.ts";

export default function GR_AI_CONTINUE_START_ACK(op: number, statusRaw: 0 = 0): Packet {
  if (statusRaw !== 0) {
    throw new RangeError("929 only the dormant status=0 arm is safe without a revival payload");
  }
  return new Packet(op).u8(statusRaw);
}
