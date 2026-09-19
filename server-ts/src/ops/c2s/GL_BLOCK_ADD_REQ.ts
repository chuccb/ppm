/**
 * 996 GL_BLOCK_ADD_REQ -> 997 GL_BLOCK_ADD_ACK.
 *
 * Native builder `sub_567E70(lpString)`: ctor(996) then one ANSI string via
 * `sub_5926F0` (same encoding family as every other nickname field, e.g. the
 * 429 friend key; see PACKETS.md stream table). No other payload.
 *
 * Server semantics (all mirrored from proven client arms):
 *   - asking to block the requester's own nickname answers
 *     `BlockAddResult.CannotBlockSelf` (client msgtable 1330);
 *   - a nickname with no player row answers `NoSuchAccount` (msgtable 282);
 *   - an existing entry answers `Duplicate` (msgtable 1323);
 *   - otherwise the row is inserted with the current epoch-ms and the reply is
 *     `Success`; the client then immediately re-requests the list snapshot
 *     (1000 -> 1003), which the store already reflects.
 * The capacity-full arm (997 code 3) exists in the client but its numeric
 * limit is unrecoverable, so this server deliberately never emits it rather
 * than inventing one.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";
import { BlockAddResult } from "../s2c/GL_BLOCK_ADD_ACK.ts";

export default function GL_BLOCK_ADD_REQ(r: Reader, connection: Connection): void {
  const nickname = r.str();
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 996`);

  if (connection.accountId === null) {
    connection.reply("GL_BLOCK_ADD_ACK", BlockAddResult.NoSuchAccount);
    return;
  }
  const identity = connection.config.store.ensurePlayerIdentity(connection.accountId);
  if (identity === null) throw new RangeError("account identity must exist after ensure");

  if (nickname === identity.nickname) {
    connection.reply("GL_BLOCK_ADD_ACK", BlockAddResult.CannotBlockSelf);
    return;
  }
  if (connection.config.store.getMyInfoByNickname(nickname) === null) {
    connection.reply("GL_BLOCK_ADD_ACK", BlockAddResult.NoSuchAccount);
    return;
  }
  if (connection.config.store.blocklistEntry(identity.userId, nickname) !== null) {
    connection.reply("GL_BLOCK_ADD_ACK", BlockAddResult.Duplicate);
    return;
  }
  connection.config.store.blocklistAdd(identity.userId, nickname, Date.now());
  connection.reply("GL_BLOCK_ADD_ACK", BlockAddResult.Success);
}
