/** 252 -> 253: empty compatibility ACK; no native success payload was recovered. */

import { Packet } from "../../packet.ts";

export default function GL_SHOPIN_ACK(op: number): Packet {
  return new Packet(op);
}
