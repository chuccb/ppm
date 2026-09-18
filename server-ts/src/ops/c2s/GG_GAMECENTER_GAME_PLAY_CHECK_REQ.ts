/**
 * 478 GG_GAMECENTER_GAME_PLAY_CHECK_REQ — mini-game anti-cheat
 * heartbeat (builder sub_564A40 @160362: ctor(478) -> sub_592580 raw
 * 0x24 -> send; wire = exactly 36 bytes assembled by sub_76EA70 from
 * stage/mode bits, timers and local values).
 *
 * Native consumer search: NO `case 479`, no `== 479`, no ctor 479
 * anywhere in this client build — the heartbeat is fire-and-forget,
 * so the client never consumes a 479 ACK (precedent: silent 689).
 *
 * TS policy: parse the 36-byte check block strictly and deliberately
 * stay silent — no ACK is ever emitted for it.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GG_GAMECENTER_GAME_PLAY_CHECK_REQ(r: Reader, _connection: Connection): void {
  if (r.remaining !== 36) {
    throw new RangeError(`478 expects exactly 36 bytes of check data, got ${r.remaining}`);
  }
  r.raw(36);
  // Deliberately silent: this client build has no 479 consumer.
}
