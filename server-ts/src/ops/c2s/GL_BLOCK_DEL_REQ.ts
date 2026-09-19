/**
 * 998 GL_BLOCK_DEL_REQ -> 999 GL_BLOCK_DEL_ACK.
 *
 * Native builder `sub_568030(lpString)`: ctor(998) then one ANSI nickname
 * string (`sub_5926F0`). No other payload.
 *
 * Server semantics (mirrored from proven client arms):
 *   - no entry with that nickname answers `BlockDelResult.NotFound` (msg 182);
 *   - an entry younger than `BLOCKLIST_REMOVE_COOLDOWN_MS` answers `Cooldown`
 *     (msg 1326's official "24時間" text — the constant lives in store.ts);
 *   - otherwise the row is deleted and the client deletes it locally as well
 *     (999 code 0 consumes the echoed nickname for its message strings).
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";
import { BlockDelResult } from "../s2c/GL_BLOCK_DEL_ACK.ts";
import { BLOCKLIST_REMOVE_COOLDOWN_MS } from "../../store.ts";

export default function GL_BLOCK_DEL_REQ(r: Reader, connection: Connection): void {
  const nickname = r.str();
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 998`);

  if (connection.accountId === null) {
    connection.reply("GL_BLOCK_DEL_ACK", BlockDelResult.NotFound, nickname);
    return;
  }
  const identity = connection.config.store.ensurePlayerIdentity(connection.accountId);
  if (identity === null) throw new RangeError("account identity must exist after ensure");

  const entry = connection.config.store.blocklistEntry(identity.userId, nickname);
  if (entry === null) {
    connection.reply("GL_BLOCK_DEL_ACK", BlockDelResult.NotFound, nickname);
    return;
  }
  if (Date.now() - entry.addedAt < BLOCKLIST_REMOVE_COOLDOWN_MS) {
    connection.reply("GL_BLOCK_DEL_ACK", BlockDelResult.Cooldown, nickname);
    return;
  }
  if (!connection.config.store.blocklistRemove(identity.userId, nickname)) {
    throw new RangeError("blocklist row vanished between lookup and delete");
  }
  connection.reply("GL_BLOCK_DEL_ACK", BlockDelResult.Success, nickname);
}
