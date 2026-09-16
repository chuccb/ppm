/** 199 is an empty request; the current Store exposes an empty page at index 0. */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GL_MYITEM_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 199`);
  connection.reply("GL_MYITEM_ACK");
}
