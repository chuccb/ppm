/** 433 is an empty request for the current user's friend list. */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GL_FRIEND_LIST_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 433`);
  const player = connection.accountId === null
    ? null
    : connection.config.store.ensurePlayer(connection.accountId);
  connection.reply("GL_FRIEND_LIST_ACK", player?.nickname ?? "");
}
