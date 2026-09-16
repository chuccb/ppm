/** 105 -> 106: an empty user list has no optional tail. */

import { Packet } from "../../packet.ts";

export default function GL_USERLIST_ACK(op: number): Packet {
  return new Packet(op).u16(0);
}
