/** 246 requests another player's public profile by nickname. */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GL_CLIENTINFO_REQ(r: Reader, connection: Connection): void {
  const nickname = r.str();
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 246`);
  connection.reply("GL_CLIENTINFO_ACK", connection.config.store.getPlayerByNickname(nickname));
}
