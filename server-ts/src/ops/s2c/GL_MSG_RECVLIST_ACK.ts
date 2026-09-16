/**
 * 425 -> 426 empty inbox page.
 *
 * A zero count omits every optional message record; no message policy is
 * invented until the Store has a mailbox model.
 */

import { Packet } from "../../packet.ts";

export default function GL_MSG_RECVLIST_ACK(op: number, self = ""): Packet {
  return new Packet(op).u16(0).str(self).u8(0);
}
