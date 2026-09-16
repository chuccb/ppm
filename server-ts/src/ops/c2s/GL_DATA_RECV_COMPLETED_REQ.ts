/** 834 reports that the client finished receiving its lobby data. */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GL_DATA_RECV_COMPLETED_REQ(r: Reader, connection: Connection): void {
  r.s32();
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 834`);
  connection.reply("GL_DATA_RECV_COMPLETED_ACK");
}
