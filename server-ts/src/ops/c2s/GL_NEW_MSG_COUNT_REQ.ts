/**
 * 783 asks how many unread messages exist (builder `sub_5643E0`: empty).
 *
 * Native 784 consumer `sub_564480` stores the s32 into `dword_F0C104`
 * and toggles the new-mail UI indicator with (count != 0).
 *
 * TS policy: this server never issues mail (the 426 mailbox is always
 * empty), so the unread count is exactly 0 — the proven "no indicator"
 * arm — and replies with the s32 zero frame.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GL_NEW_MSG_COUNT_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 783`);
  connection.reply("GL_NEW_MSG_COUNT_ACK", 0);
}
