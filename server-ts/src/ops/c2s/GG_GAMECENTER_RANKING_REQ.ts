/**
 * 480 GG_GAMECENTER_RANKING_REQ — query mini-game rankings (builder
 * sub_585320 @175599, gated by the game-center state so it never sends
 * while a session is live): ctor(480) -> sub_5929E0 2-byte game_id ->
 * sub_592920 1-byte mode -> send; wire = exactly 3 bytes.
 *
 * Native 481 consumer sub_585080 read order:
 *   u16 game_id, u8 v18, s16 v13, s32 v14,
 *   u8 top3_cnt (only processed while <= 3), top3_cnt x 0x38-byte rows,
 *   u8 top10_cnt (only processed while <= 0xA), top10_cnt x 0x38 rows
 *
 * TS policy: no ranking persistence -> zero boards: game_id echoed,
 * v18/v13/v14 zero, both counters zero (11 bytes), which traverses the
 * <=3 / <=0xA guards harmlessly.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GG_GAMECENTER_RANKING_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 3) {
    throw new RangeError(`480 expects exactly 3 bytes (s16 game_id, u8 mode), got ${r.remaining}`);
  }
  const gameId = r.s16();
  r.u8(); // mode
  connection.reply("GG_GAMECENTER_RANKING_ACK", gameId);
}
