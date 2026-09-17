/** 105 -> 106: the native 16-bit gate is zero, so no user-list fields follow. */

import { Packet } from "../../packet.ts";

export default function GL_USERLIST_ACK(op: number): Packet {
  return new Packet(op).u16(0); // native 16-bit gate; record count would be the later u8
}
