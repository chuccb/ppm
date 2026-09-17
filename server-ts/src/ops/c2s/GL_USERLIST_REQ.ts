/** 105 carries the native u8 refresh trigger (the client emits 1). */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GL_USERLIST_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 1) throw new RangeError(`105 expects one refresh byte, got ${r.remaining}`);
  const refreshTrigger = r.u8();
  if (refreshTrigger !== 1) {
    throw new RangeError(`105 native writer emits refresh byte 1, got ${refreshTrigger}`);
  }
  // The native value domain is proven here; its status/filter meaning is not.
  connection.reply("GL_USERLIST_ACK");
}
