/**
 * 900 -> 901: consumer-safe capsule denial.
 *
 * sub_9A1A30 consumes `{u8 status, s32 count, 3×s32 tails}` unconditionally;
 * status nonzero + count zero means no award records and no local wallet or
 * reward mutation. The four trailer words still must exist for the reader.
 */

import { Packet } from "../../packet.ts";

export default function GS_CAPSULEMACHINE_START_ACK(op: number): Packet {
  return new Packet(op).u8(1).s32(0).s32(0).s32(0).s32(0);
}
