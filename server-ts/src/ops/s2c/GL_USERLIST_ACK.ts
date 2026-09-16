/** 105 -> 106: the u16 count is zero, so no user-list fields follow. */

import { Packet } from "../../packet.ts";

export default function GL_USERLIST_ACK(op: number): Packet {
  return new Packet(op).u16(0); // user record count
}
