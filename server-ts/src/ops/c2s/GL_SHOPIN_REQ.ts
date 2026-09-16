/** 252 is empty and locally moves the client into shop state 3. */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GL_SHOPIN_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 252`);
  // 253 has no recovered native consumer; this reply is an interoperability choice.
  connection.reply("GL_SHOPIN_ACK");
}
