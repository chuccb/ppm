/**
 * 685 tutorial index query (builder sub_55C6F0: empty payload).
 *
 * Native 686 consumer sub_55C790 stores one s32 into the global
 * tutorial marker and refreshes the tutorial UI; sub_4422B0 compares
 * it against the literal 145 (hides the TUTO_NEW badge on equality).
 *
 * TS policy: the marker is per-account progression state, so it now
 * lives in the player table (synced by 689). An unbound identity falls
 * back to the fresh state 0 — same as before, but without discarding
 * progress the client reports.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GL_TUTORIALINDEX_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 685`);
  let tutorialIndex = 0;
  if (connection.accountId != null) {
    const myInfo = connection.config.store.ensurePlayerIdentity(connection.accountId);
    if (myInfo) tutorialIndex = connection.config.store.getTutorialIndex(myInfo.userId);
  }
  connection.reply("GL_TUTORIALINDEX_ACK", tutorialIndex);
}
