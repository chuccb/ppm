/**
 * 485 -> 486 GL_GET_GAMEROOM_PROGRESSTIME_ACK (consumer sub_56AE30).
 *
 * The single byte `u8 n3 = 3` normalises onto the consumer's silent
 * "unknown" arm (renderer skipped). This server has no room-progress
 * model, so this is the only arm it serialises (wire "03").
 */

import { Packet } from "../../packet.ts";

export default function GL_GET_GAMEROOM_PROGRESSTIME_ACK(op: number): Packet {
  return new Packet(op).u8(3);
}
