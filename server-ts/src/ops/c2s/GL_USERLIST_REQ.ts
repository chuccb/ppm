/** 105 carries one raw refresh byte; an empty list needs no optional ACK tail. */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GL_USERLIST_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 1) throw new RangeError(`105 expects one refresh byte, got ${r.remaining}`);
  r.s8();
  connection.reply("GL_USERLIST_ACK");
}
