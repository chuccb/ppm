/** 252 -> 253: the recovered interoperability ACK has an empty payload. */

import { Packet } from "../../packet.ts";

export default function GL_SHOPIN_ACK(op: number): Packet {
  return new Packet(op);
}
