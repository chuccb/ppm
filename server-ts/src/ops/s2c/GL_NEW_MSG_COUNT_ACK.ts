/**
 * 783 -> 784 unread-mail count (sub_564480 consumer).
 *
 * Wire: `s32 count`; the consumer stores it into `dword_F0C104` and
 * toggles the new-mail indicator with (count != 0). This server has no
 * mailbox, so only 0 is ever emitted.
 */

import { Packet } from "../../packet.ts";

export default function GL_NEW_MSG_COUNT_ACK(op: number, count: number): Packet {
  return new Packet(op).s32(count);
}
