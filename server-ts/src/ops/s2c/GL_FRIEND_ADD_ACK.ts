/**
 * 429 -> 430 friend-add result (sub_55AA90 consumer).
 *
 * Wire: `{u8 statusRaw, str key}`. The key echoes the request nickname;
 * the native read local is a 24-byte stack area, so the send-side gate
 * (429 builder: `strlen <= 23` non-empty) bounds the wire string.
 *
 * Status branches (native): 0 inserts the key into the 100-entry friend
 * table (`sub_537F60`) and shows resource `0x1E7`; 1..4 select
 * `0x1E8/0x1E9/0x1EA/0x1EB` (self / duplicate / success / reconnect per
 * recovered stringtable entries) and every other value selects `0x1EC`
 * (account not registered). Regardless of the branch the client then
 * sends an empty 433 refresh request (`sub_55AF20`). The complete status
 * enum is not a server policy proof; do not invent other codes.
 */

import { Packet } from "../../packet.ts";

/** Native 429/431 send-side gate: non-empty key with `strlen <= 23`. */

export default function GL_FRIEND_ADD_ACK(op: number, statusRaw: number, key: string): Packet {
  return new Packet(op)
    .u8(statusRaw)
    .str(key); // native 24-byte ACK read local
}
