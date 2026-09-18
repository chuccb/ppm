/**
 * 476 GG_GAMECENTER_GAME_END_REQ — mini-game result submission
 * (builder sub_564930 @160332: sub_5929E0 2-byte game_id, then
 * sub_592580 raw 0x18 and 0x2C blobs; wire = exactly 70 bytes).
 *
 * Native 477 consumer sub_76E450 reads the FULL 137-byte record
 * unconditionally (no arms): u16 game_id, raw32, raw44, u16, s32,
 * raw24, raw8, 4 x s32 rewards, s8, u8, u8, s8, s8 — feeding the
 * GunShooting local stats and wallets (adds zero when every field is
 * zero).
 *
 * TS policy: no session/ranking persistence, so the ACK is the zero
 * settlement: game_id echoed, every other byte zero.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GG_GAMECENTER_GAME_END_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 70) {
    throw new RangeError(`476 expects exactly 70 bytes (s16 game_id, raw24, raw44), got ${r.remaining}`);
  }
  const gameId = r.s16();
  r.raw(24);
  r.raw(44);
  connection.reply("GG_GAMECENTER_GAME_END_ACK", gameId);
}
