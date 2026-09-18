/**
 * 480 -> 481 GG_GAMECENTER_RANKING_ACK (consumer sub_585080).
 *
 * Wire: `u16 game_id, u8 v18, s16 v13, s32 v14, u8 top3_cnt,
 * u8 top10_cnt` = 11 bytes with both 0x38-row lists empty (counters
 * zero pass the <=3 / <=0xA guards). With no ranking persistence this
 * server always emits the zero board with the echoed game id.
 */

import { Packet } from "../../packet.ts";

export default function GG_GAMECENTER_RANKING_ACK(
  op: number,
  gameId: number,
): Packet {
  return new Packet(op)
    .u16(gameId)
    .u8(0)
    .s16(0)
    .s32(0)
    .u8(0)
    .u8(0);
}
