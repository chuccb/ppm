/**
 * 472 -> 473 GL_GAMECENTER_REC_ACK (consumer sub_584910).
 *
 * Wire (native order, conditional raw arms off under the all-zero
 * policy): `u16 game_id, s32 high_score, u8 top3_cnt, u8 top10_cnt,
 * u8 v24, u8 v35, s16 v28, s32 v30, 16x raw zero, u8 v23, u8 v31,
 * u16 v32Raw, u16 v21` = 38 bytes with empty 0x38-row lists.
 * This server keeps no mini-game records, so the frame is the empty
 * board with only the echoed game id nonzero.
 */

import { Packet } from "../../packet.ts";

export default function GL_GAMECENTER_REC_ACK(
  op: number,
  gameId: number,
): Packet {
  if (!Number.isSafeInteger(gameId) || gameId < 0 || gameId > 0xffff) {
    throw new RangeError("473 game_id must be a u16 raw value");
  }
  const p = new Packet(op)
    .u16(gameId)
    .s32(0)      // high_score
    .u8(0)       // top3_cnt  (0x38-row list empty)
    .u8(0)       // top10_cnt (0x38-row list empty)
    .u8(0)       // v24
    .u8(0)       // v35 (no trailing 0x20 raw)
    .s16(0)      // v28
    .s32(0);     // v30
  for (let i = 0; i < 16; i++) p.u8(0); // 0x10 raw
  return p
    .u8(0)       // v23 (no trailing 0x2C raw)
    .u8(0)       // v31
    .u16(0)      // v32Raw
    .u16(0);     // v21
}
