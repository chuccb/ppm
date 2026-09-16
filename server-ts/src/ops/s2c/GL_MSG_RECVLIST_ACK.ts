/**
 * 425 -> 426 empty message-list projection.
 *
 * A zero count omits every optional message record; no mailbox/page policy is
 * invented until the Store has a message model.
 */

import { Packet } from "../../packet.ts";

export default function GL_MSG_RECVLIST_ACK(op: number, self = ""): Packet {
  return new Packet(op).u16(0).str(self).u8(0);
}
