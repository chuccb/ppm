/**
 * 1000 GL_BLOCK_LIST_REQ -> 1001 GL_BLOCK_LIST_ACK.
 *
 * Native builder `sub_567CB0`: a bare ctor(1000) with *no payload at all*
 * (request-triggered roster refresh; the client auto-sends it right after a
 * successful 996 add ACK — `sub_567F20`'s code-0 arm calls `sub_567CB0()`
 * immediately).
 *
 * The reply is the full replacement snapshot from the store. Every row
 * carries BLOCK_LIST_ENTRY_FLAG_NONE (the recovery-visible 0x80000000 mark
 * has no located consumer; see the ACK module).
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";
import { BLOCK_LIST_ENTRY_FLAG_NONE } from "../s2c/GL_BLOCK_LIST_ACK.ts";

export default function GL_BLOCK_LIST_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 1000`);

  if (connection.accountId === null) {
    connection.reply("GL_BLOCK_LIST_ACK", []);
    return;
  }
  const identity = connection.config.store.ensurePlayerIdentity(connection.accountId);
  if (identity === null) throw new RangeError("account identity must exist after ensure");

  const rows = connection.config.store.blocklistEntries(identity.userId);
  connection.reply(
    "GL_BLOCK_LIST_ACK",
    rows.map((row) => ({ flags: BLOCK_LIST_ENTRY_FLAG_NONE, nickname: row.nickname })),
  );
}
