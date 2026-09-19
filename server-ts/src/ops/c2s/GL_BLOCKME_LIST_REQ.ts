/**
 * 1002 GL_BLOCKME_LIST_REQ -> 1003 GL_BLOCKME_LIST_ACK.
 *
 * Native builder `sub_567B30`: a bare ctor(1002) with no payload (the "who
 * has blocked me" roster refresh).
 *
 * Server projects every other player whose blocklist currently contains the
 * requester's nickname; the ACK carries nicknames only (no per-entry flag on
 * this direction — see the ACK module and the LAYOUTS 黑單家族 rows).
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GL_BLOCKME_LIST_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 1002`);

  if (connection.accountId === null) {
    connection.reply("GL_BLOCKME_LIST_ACK", []);
    return;
  }
  const identity = connection.config.store.ensurePlayerIdentity(connection.accountId);
  if (identity === null) throw new RangeError("account identity must exist after ensure");

  const rows = connection.config.store.blocklistBlockedBy(identity.nickname);
  connection.reply("GL_BLOCKME_LIST_ACK", rows.map((row) => row.nickname));
}
