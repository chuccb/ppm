/**
 * 441 asks where one friend is (tutorial/master/lobby/room).
 *
 * Native builder `sub_55B940`: wire is exactly `str nick` with no
 * length gate at the builder (the nick comes from the friend-list UI,
 * so the defensive cap is the proven 20-byte friend-table stride).
 *
 * TS policy: with no friend-presence model the where-lookup can never
 * resolve; the proven failure arm `status = 0` selects resource `0x21D`
 * (the retriable "information not found" dialog) and — as the binary
 * shows for every non-1 status — no further bytes are read, so the ACK
 * is emitted without the conditional triple.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";
const FRIEND_NICKNAME_MAX_BYTES = 20; // sub_537F60 stride-21 slot, including NUL

export default function GL_FRIEND_WHERE_REQ(r: Reader, connection: Connection): void {
  const nick = r.str();
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 441`);
  if (nick.length === 0) throw new RangeError("441 nick must be non-empty (friend-table key)");
  if (nick.length > FRIEND_NICKNAME_MAX_BYTES) {
    throw new RangeError("441 nick exceeds the native 20-byte friend-table stride");
  }
  connection.reply("GL_FRIEND_WHERE_ACK", 0);
}
