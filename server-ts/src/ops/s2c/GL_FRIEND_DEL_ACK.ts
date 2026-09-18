/**
 * 431 -> 432 friend-delete result (sub_55AE10 consumer).
 *
 * Wire: `{u8 statusRaw, str key}`; the key echoes the request nickname
 * (native 24-byte ACK read local; 431 send-side gate `strlen <= 23`
 * non-empty).
 *
 * Status branches (native): 0 removes the key (`sub_538010`) and sends an
 * empty 433 refresh (`sub_55AF20`); 1 selects resource `0x1ED`
 * (already-registered-user text); 2 selects `0x1EE` (friend-list
 * registration failure text). Both nonzero arms leave the table and the
 * refresh untouched and end with the same UI tail.
 */

import { Packet } from "../../packet.ts";

export default function GL_FRIEND_DEL_ACK(op: number, statusRaw: number, key: string): Packet {
  return new Packet(op)
    .u8(statusRaw)
    .str(key); // native 24-byte ACK read local
}
