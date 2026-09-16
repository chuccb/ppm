/**
 * 433 -> 434 empty friend list.
 *
 * The friend table is not part of the current Store. The wire still requires
 * the unresolved 2-byte header, local nickname, and zero count before the
 * client can continue. The header is not guessed as a page or status.
 */

import { Packet } from "../../packet.ts";

export default function GL_FRIEND_LIST_ACK(op: number, self = ""): Packet {
  return new Packet(op)
    .u16(0) // native header; semantics unresolved
    .str(self) // request-account display name, not a friend-owner ID
    .u8(0); // friend record count
}
