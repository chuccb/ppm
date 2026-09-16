/** 425 requests one inbox page by a signed page index. */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GL_MSG_RECVLIST_REQ(r: Reader, connection: Connection): void {
  r.s32();
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 425`);
  const player = connection.accountId === null
    ? null
    : connection.config.store.ensurePlayer(connection.accountId);
  connection.reply("GL_MSG_RECVLIST_ACK", player?.nickname ?? "");
}
