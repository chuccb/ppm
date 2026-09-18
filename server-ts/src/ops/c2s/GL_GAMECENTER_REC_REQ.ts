/**
 * 472 GL_GAMECENTER_REC_REQ — query a game-center mini-game record
 * (builder sub_584850 @175275: ctor(472) -> sub_5929E0 2-byte write ->
 * send; wire = exactly 2 bytes game_id; local flag byte_EA12F4 set from
 * the caller, business meaning unresolved).
 *
 * Native 473 consumer sub_584910 (line-level re-read; accessor widths
 * 592A00 = 2B read, 592A40 = 4B, 592940 = 1B, 592500 = raw width given):
 *   u16 game_id, s32 high_score,
 *   u8 top3_cnt,  top3_cnt  x 0x38-byte rows,
 *   u8 top10_cnt, top10_cnt x 0x38-byte rows,
 *   u8 v24, u8 v35, [v35 != 0: 0x20 raw],
 *   s16 v28, s32 v30, 0x10 raw,
 *   u8 v23, [v23 != 0: 0x2C raw],
 *   u8 v31, u16 v32Raw, u16 v21
 *
 * TS policy: no game-center record persistence, so the client always
 * receives the empty board: game_id echoed, everything else zero —
 * the two 0x38-row lists empty, all conditional arms (v35, v23) off.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GL_GAMECENTER_REC_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 2) {
    throw new RangeError(`472 expects exactly 2 bytes (game_id), got ${r.remaining}`);
  }
  const gameId = r.u16();
  connection.reply("GL_GAMECENTER_REC_ACK", gameId);
}
