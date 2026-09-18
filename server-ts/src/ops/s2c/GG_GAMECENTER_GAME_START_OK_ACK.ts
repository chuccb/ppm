/**
 * 483 -> 484 GG_GAMECENTER_GAME_START_OK_ACK (consumer sub_584F70;
 * no arms, all nine bytes read).
 *
 * Wire: `u16 gameIdRaw, u8 status, u16 game_id, s32 resultRaw`.
 * This server: status = 1, game_id echoed, resultRaw = 0.
 */

import { Packet } from "../../packet.ts";

export default function GG_GAMECENTER_GAME_START_OK_ACK(
  op: number,
  gameId: number,
): Packet {
  return new Packet(op)
    .u16(gameId)
    .u8(1)
    .u16(gameId)
    .s32(0);
}
