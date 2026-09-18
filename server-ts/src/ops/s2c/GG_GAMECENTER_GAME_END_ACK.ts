/**
 * 476 -> 477 GG_GAMECENTER_GAME_END_ACK (consumer sub_76E450).
 *
 * Wire: the full native 137-byte record `u16 game_id, raw32, raw44,
 * u16, s32, raw24, raw8, s32 score, s32 reward_gp, s32 reward_exp,
 * s32 rank, s8, u8, u8, s8, s8`. No branch exists. With no session
 * persistence this server always emits the zero settlement: the echoed
 * game id is the only nonzero field.
 */

import { Packet } from "../../packet.ts";

export default function GG_GAMECENTER_GAME_END_ACK(
  op: number,
  gameId: number,
): Packet {
  const p = new Packet(op).u16(gameId);
  for (let i = 0; i < 32; i++) p.u8(0); // raw32
  for (let i = 0; i < 44; i++) p.u8(0); // raw44
  p.u16(0).s32(0); // u16 + s32
  for (let i = 0; i < 24; i++) p.u8(0); // raw24
  for (let i = 0; i < 8; i++) p.u8(0); // raw8
  p.s32(0).s32(0).s32(0).s32(0); // score, gp, exp, rank
  p.s8(0).u8(0).u8(0).s8(0).s8(0);
  return p;
}
