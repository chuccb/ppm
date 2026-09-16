/** 252 is empty; the compatibility ACK is 253 with an empty payload. */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GL_SHOPIN_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 252`);
  connection.reply("GL_SHOPIN_ACK");
}
