/**
 * 476 -> 477 GG_GAMECENTER_GAME_END_ACK (consumer sub_76E450).
 *
 * Wire: the full native 137-byte record, no branch:
 *   u16 game_id, raw32, raw44, u16, s32, raw24, raw8,
 *   s32 score, s32 reward_gp, s32 reward_exp, s32 rank,
 *   s8, u8, u8, s8, s8
 *
 * Field semantics per the sub_76E450 audit: the echoed game id pairs
 * the settlement with the game session; `score` is the table score the
 * client prints, `reward_gp`/`reward_exp` are credited to the local
 * player, `rank` drives the ranking row highlight; the five trailing
 * bytes and the raw blobs are carried verbatim into the client game-
 * center table (their per-byte domains stay unresolved, so zeros are
 * the only non-fabricated values there).
 *
 * With no session persistence the default `settlement` below is the
 * complete zero settlement: the echoed game id is the only nonzero
 * field and no rewards are claimed. A real settlement can be supplied
 * field-by-field once a game-session store exists.
 */

import { Packet } from "../../packet.ts";

/** Meaningful settlement fields; everything else of the 137-byte record stays zero. */
export interface GameEndSettlement {
  readonly score?: number;
  readonly rewardGp?: number;
  readonly rewardExp?: number;
  readonly rank?: number;
}

export default function GG_GAMECENTER_GAME_END_ACK(
  op: number,
  gameId: number,
  settlement: GameEndSettlement = {},
): Packet {
  const p = new Packet(op).u16(gameId);
  for (let i = 0; i < 32; i++) p.u8(0); // raw32: verbatim table blob, domains unresolved
  for (let i = 0; i < 44; i++) p.u8(0); // raw44: verbatim table blob, domains unresolved
  p.u16(0).s32(0); // header u16 + s32, consumed but semantically unreachable today
  for (let i = 0; i < 24; i++) p.u8(0); // raw24
  for (let i = 0; i < 8; i++) p.u8(0); // raw8
  p.s32(settlement.score ?? 0)
    .s32(settlement.rewardGp ?? 0)
    .s32(settlement.rewardExp ?? 0)
    .s32(settlement.rank ?? 0);
  p.s8(0).u8(0).u8(0).s8(0).s8(0); // five trailing bytes of the route string, inert at zero
  return p;
}
