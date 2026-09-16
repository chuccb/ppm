/**
 * 433 -> 434 empty friend list.
 *
 * The friend table is not part of the current Store. The wire still requires
 * the local nickname and a zero count before the client can continue.
 */

import { Packet } from "../../packet.ts";

export default function GL_FRIEND_LIST_ACK(op: number, self = ""): Packet {
  return new Packet(op).u16(0).str(self).u8(0);
}
