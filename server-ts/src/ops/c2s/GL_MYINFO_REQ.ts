/** 197 is an empty request; the Store owns the 198 player projection. */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GL_MYINFO_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 197`);
  const player = connection.accountId === null
    ? null
    : connection.config.store.ensurePlayer(connection.accountId);
  connection.reply("GL_MYINFO_ACK", player);
}
