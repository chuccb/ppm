/** 105 carries the native s8 refresh trigger (the client emits 1). */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GL_USERLIST_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 1) throw new RangeError(`105 expects one refresh byte, got ${r.remaining}`);
  r.s8(); // trigger value; no separate server-side meaning is proven
  connection.reply("GL_USERLIST_ACK");
}
