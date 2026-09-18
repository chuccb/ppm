/**
 * 474 GG_GAMECENTER_GAME_START_REQ — start a game-center mini-game
 * (builder sub_584DB0 @175429: ctor(474) -> sub_5929E0 2-byte game_id
 * -> sub_592920 1-byte stage -> send; wire = exactly 3 bytes).
 *
 * Native 475 consumer sub_584E80 reads NOTHING from the packet — it
 * only refreshes the game-center list control (sub_457380). The
 * documented `u8 status, s16 game_id, u8 stage` triple sits below the
 * parse surface.
 *
 * TS policy: no mini-game sessions exist, but since the client never
 * consumes the ACK body, the least-surprising frame echoes the request
 * with the documented success status 1.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GG_GAMECENTER_GAME_START_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 3) {
    throw new RangeError(`474 expects exactly 3 bytes (s16 game_id, u8 stage), got ${r.remaining}`);
  }
  const gameId = r.s16();
  const stage = r.u8();
  connection.reply("GG_GAMECENTER_GAME_START_ACK", gameId, stage);
}
