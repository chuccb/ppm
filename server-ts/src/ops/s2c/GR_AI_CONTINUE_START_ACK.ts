/**
 * 928 -> 929 GR_AI_CONTINUE_START_ACK (consumer sub_761E90).
 *
 * Wire: `u8 statusRaw`. Only statusRaw == 1 makes the client read the
 * full revival payload (u8, s32, string, s32, s32); any value != 1 logs
 * the result and ends the frame. This server cannot fabricate a revival
 * payload (no PVE session state), so it always emits status = 0
 * (wire "00").
 */

import { Packet } from "../../packet.ts";

export default function GR_AI_CONTINUE_START_ACK(op: number): Packet {
  return new Packet(op).u8(0);
}
