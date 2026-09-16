/**
 * 425 -> 426 empty message-list projection.
 *
 * The native reader consumes the context string into a local buffer but has no
 * recovered consumer for it; the argument is therefore only a bounded
 * compatibility projection.
 *
 * A zero count omits every optional message record; no mailbox/page policy is
 * invented until the Store has a message model.
 */

import { Packet } from "../../packet.ts";

const CONTEXT_STRING_MAX_BYTES = 23; // native local char[24], including NUL

export default function GL_MSG_RECVLIST_ACK(op: number, contextString = ""): Packet {
  if (typeof contextString !== "string") throw new TypeError("426 context string must be a string");
  if (contextString.length > CONTEXT_STRING_MAX_BYTES) {
    throw new RangeError("426 context string must fit native char[24]");
  }
  return new Packet(op).u16(0).str(contextString).u8(0);
}
