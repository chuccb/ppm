/** 107 is an empty room-list refresh request. */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GL_GAMEROOMINFO_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 107`);
  connection.reply("GL_GAMEROOMINFO_ACK");
}
