/**
 * 433 -> 434 empty friend list.
 *
 * The friend table is not part of the current Store. The wire still requires
 * the unresolved 2-byte header, context string, and zero count before the
 * client can continue. The header is not guessed as a page or status.
 */

import { Packet } from "../../packet.ts";

const CONTEXT_STRING_MAX_BYTES = 20; // native local char[21], including NUL

export default function GL_FRIEND_LIST_ACK(op: number, contextString = ""): Packet {
  if (typeof contextString !== "string") throw new TypeError("434 context string must be a string");
  if (contextString.length > CONTEXT_STRING_MAX_BYTES) {
    throw new RangeError("434 context string must fit native char[21]");
  }
  return new Packet(op)
    .u16(0) // native header; semantics unresolved
    .str(contextString) // bounded compatibility string; native 434 does not prove its owner/display semantics
    .u8(0); // friend record count
}
