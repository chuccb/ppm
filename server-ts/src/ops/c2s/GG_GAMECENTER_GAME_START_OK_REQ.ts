/**
 * 483 GG_GAMECENTER_GAME_START_OK_REQ — confirm the mini-game start
 * (builder sub_584EC0 @175469: ctor(483) -> sub_5929E0 2-byte game_id
 * -> send; wire = exactly 2 bytes).
 *
 * Native 484 consumer sub_584F70 reads all nine bytes with no arms:
 * `u16 gameIdRaw, u8 status, u16 game_id, s32 resultRaw` (the status byte is
 * read but never used) and then fires the UI probe
 * sub_5392A0(byte_EE8968, game_id, resultRaw).
 *
 * TS policy: no session model, so the legal answer echoes the game id
 * and reports the documented success: `status = 1, game_id echo,
 * resultRaw = 0` (9 bytes).
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GG_GAMECENTER_GAME_START_OK_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 2) {
    throw new RangeError(`483 expects exactly 2 bytes (s16 game_id), got ${r.remaining}`);
  }
  const gameId = r.s16();
  connection.reply("GG_GAMECENTER_GAME_START_OK_ACK", gameId & 0xffff);
}
