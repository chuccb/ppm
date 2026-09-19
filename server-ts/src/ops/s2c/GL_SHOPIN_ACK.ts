/** 252 -> 253: empty compatibility ACK; no native success payload was
 * recovered. Registry-only name: this client build ships no native 253
 * endpoint at all (docs/LAYOUTS.md Part IV), so the empty frame is the
 * complete contract. */

import { Packet } from "../../packet.ts";

export default function GL_SHOPIN_ACK(op: number): Packet {
  return new Packet(op);
}
