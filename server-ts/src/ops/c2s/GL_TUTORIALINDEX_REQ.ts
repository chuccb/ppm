/**
 * 685 asks which tutorial step the account is on (builder sub_55C6F0:
 * empty payload on the GL lobby socket).
 *
 * Native 686 consumer sub_55C790 reads one s32 into the global
 * tutorial-index (n145_0) and refreshes the tutorial UI.
 *
 * TS policy: there is no per-account tutorial progression state, so the
 * tutorial board starts blank — reply s32(0), the proven "empty step"
 * frame. 689 metadata echo: not consumed by any native client case =>
 * no 690 reply is ever emitted.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GL_TUTORIALINDEX_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 685`);
  connection.reply("GL_TUTORIALINDEX_ACK", 0);
}
