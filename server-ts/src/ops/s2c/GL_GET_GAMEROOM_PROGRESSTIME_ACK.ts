/**
 * 485 -> 486 GL_GET_GAMEROOM_PROGRESSTIME_ACK (consumer sub_56AE30).
 *
 * Wire under the TS policy: the single byte `u8 n3 = 3`, which the
 * consumer normalises onto its silent "unknown" arm (renderer skipped).
 * Every other n3 value requires room progress data this server has no
 * model of, so they are rejected here.
 */

import { Packet } from "../../packet.ts";

export default function GL_GET_GAMEROOM_PROGRESSTIME_ACK(
  op: number,
  n3: 3 = 3,
): Packet {
  if (n3 !== 3) {
    throw new RangeError("486 of this server is always n3 = 3 (no room-progress model)");
  }
  return new Packet(op).u8(n3);
}
