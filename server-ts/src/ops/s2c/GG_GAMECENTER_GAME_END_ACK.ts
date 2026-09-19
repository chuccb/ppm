/**
 * 476 -> 477 GG_GAMECENTER_GAME_END_ACK (consumer sub_76E450, fully
 * re-read line level 2026-09-19).
 *
 * Wire: the full native 137-byte record, no branch:
 *   u16 game_id, raw 0x20, raw 0x2C, u16, s32, raw 0x18, raw 8,
 *   s32 v40, s32 v22, s32 v28, s32 v44, s8 v23, u8, u8, s8 v29, s8 v43
 *
 * Proven per-field consumption in sub_76E450:
 * - v40 → `sub_8EE1D0()[2]` game-center record slot 2 (score cell)
 * - v22 → `*dword_EE8D0C += v22` and the accumulated total is class-
 *   indexed through `sub_403360` (the same exp→Class mapping as §3.8),
 *   i.e. v22 is the exp-family increment
 * - v28 → `*dword_EE8D18 = v28`, a direct wallet-global assign in the
 *   same EE8D0C..EE8D1C family as 198's cash/coupon words
 * - v44, v42, the lone u16, the raw blobs and v23: read but stored-only
 * - the trailing bytes: v25[0] → slot [6]; v29 → `byte_EE8C80` list
 *   flag; v43 → `sub_996140(sub_44D330(), v43)`
 *
 * The default frame is the semantic zero settlement (no rewards, no
 * rows changed). The settlement fields below are wired, in native
 * order, for a future game-session store — with native neutral names,
 * since "reward_gp/exp"-style naming is what the dump proves wrong.
 */

import { Packet } from "../../packet.ts";

/** Settlement cells with proven consumption sites, in native wire order. */
export interface GameEndSettlement {
  /** v40 → game-center record slot [2]. */
  readonly score?: number;
  /** v22 → accumulated into `dword_EE8D0C`; the total is class-indexed via sub_403360. */
  readonly expTotalDelta?: number;
  /** v28 → assigned into `dword_EE8D18` (wallet-family global). */
  readonly walletSet?: number;
  /** v44: read but stored-only. */
  readonly unused44?: number;
}

export default function GG_GAMECENTER_GAME_END_ACK(
  op: number,
  gameId: number,
  settlement: GameEndSettlement = {},
): Packet {
  const p = new Packet(op).u16(gameId);
  for (let i = 0; i < 32; i++) p.u8(0); // raw 0x20: verbatim blob, domains unresolved
  for (let i = 0; i < 44; i++) p.u8(0); // raw 0x2C: verbatim blob (feeds sub_76F5F0 table)
  p.u16(0).s32(0); // stored-only u16 v45 + s32 v42
  for (let i = 0; i < 24; i++) p.u8(0); // raw 0x18
  for (let i = 0; i < 8; i++) p.u8(0); // raw 8
  p.s32(settlement.score ?? 0)          // v40 -> global slot [2]
    .s32(settlement.expTotalDelta ?? 0) // v22 -> dword_EE8D0C += (class-indexed total)
    .s32(settlement.walletSet ?? 0)     // v28 -> dword_EE8D18 assign
    .s32(settlement.unused44 ?? 0);     // v44: stored-only
  p.s8(0).u8(0).u8(0).s8(0).s8(0); // v23, v25 bytes, v38, v29 (byte_EE8C80), v43 (sub_996140)
  return p;
}
