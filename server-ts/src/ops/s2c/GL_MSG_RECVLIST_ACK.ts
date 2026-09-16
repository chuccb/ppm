/**
 * 425 -> 426 empty message-list projection.
 *
 * A zero count omits every optional message record; no mailbox/page policy is
 * invented until the Store has a message model.
 */

import { Packet } from "../../packet.ts";

const SELF_NAME_MAX_BYTES = 23; // native local char[24], including NUL

export default function GL_MSG_RECVLIST_ACK(op: number, self = ""): Packet {
  if (typeof self !== "string") throw new TypeError("426 self name must be a string");
  if (self.length > SELF_NAME_MAX_BYTES) {
    throw new RangeError("426 self name must fit native char[24]");
  }
  return new Packet(op).u16(0).str(self).u8(0);
}
