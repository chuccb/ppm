/**
 * 935 -> 936 GR_AI_FEVER_START_ACK (consumer sub_7623A0).
 *
 * Wire: `u8 status, u8 flag, s32 duration_ms, u8 type` — 7 bytes,
 * always. status != 0 with duration != the client baseline dword_EE8CB4
 * only error-logs; status == 0 is the explicitly designated declined
 * arm (state=2 + flagged UI broadcast, no further reads). With no
 * fever model this server always emits the dormant declined frame:
 * seven zero bytes (wire "00000000000000").
 */

import { Packet } from "../../packet.ts";

export default function GR_AI_FEVER_START_ACK(
  op: number,
  status: 0 = 0,
  flag = 0,
  durationMs = 0,
  type = 0,
): Packet {
  if (status !== 0 || flag !== 0 || durationMs !== 0 || type !== 0) {
    throw new RangeError("936 only the dormant declined frame (7 x 0) is safe without a fever model");
  }
  return new Packet(op).u8(0).u8(0).s32(0).u8(0);
}
