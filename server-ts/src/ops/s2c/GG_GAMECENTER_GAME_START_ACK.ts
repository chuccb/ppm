/**
 * 474 -> 475 GG_GAMECENTER_GAME_START_ACK (consumer sub_584E80, which
 * reads no field — only refreshes the list control).
 *
 * Wire: `u8 status, s16 game_id, u8 stage` (documented shape). This
 * server echoes game id and stage with status = 1.
 */

import { Packet } from "../../packet.ts";

export default function GG_GAMECENTER_GAME_START_ACK(
  op: number,
  gameId: number,
  stage: number,
): Packet {
  if (!Number.isSafeInteger(gameId) || gameId < -0x8000 || gameId > 0x7fff) {
    throw new RangeError("475 game_id must fit s16");
  }
  if (!Number.isSafeInteger(stage) || stage < 0 || stage > 0xff) {
    throw new RangeError("475 stage must be a u8");
  }
  return new Packet(op).u8(1).s16(gameId).u8(stage);
}
