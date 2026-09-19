/**
 * 480 -> 481 GG_GAMECENTER_RANKING_ACK (consumer sub_585080, re-read
 * line level 2026-09-19).
 *
 * Native read order: `u16 game_id, u8 v18, u16 v13, s32 v14,
 * u8 top3_cnt, top3_cnt×raw 0x38, u8 top10_cnt, top10_cnt×raw 0x38`.
 *
 * Per-field usage in the consumer:
 * - `game_id` echoes the ranking board the client is showing.
 * - `v18` is the ONLY head byte actually consumed afterwards: it is
 *   handed to `sub_4574F0(dword_EA1260, v18)` as the list-control state
 *   selector. Its full domain is unresolved; 0 keeps the default view.
 * - `v13`/`v14` are read into locals that are never referenced again —
 *   stored-but-unconsumed wire words, so 0 is the only honest value.
 * - the counts guard the two 0x38-byte row lists (<=3 / <=0xA native
 *   caps); zero = "no recorded ranking rows", not padding.
 *
 * With no ranking persistence this server emits the semantic empty
 * board: echoed game id, default list state, no rows anywhere.
 */

import { Packet } from "../../packet.ts";

/** Local-player board head; only `listState` is provably consumed client-side. */
export interface RankingBoardHead {
  /** v18 -> sub_4574F0 list-control state selector. */
  readonly listState?: number;
  /** v13: read into a local the consumer never uses. */
  readonly unused13?: number;
  /** v14: read into a local the consumer never uses. */
  readonly unused14?: number;
}

export default function GG_GAMECENTER_RANKING_ACK(
  op: number,
  gameId: number,
  head: RankingBoardHead = {},
): Packet {
  return new Packet(op)
    .u16(gameId)
    .u8(head.listState ?? 0)   // v18 -> sub_4574F0 list state
    .u16(head.unused13 ?? 0)   // v13: stored, never consumed
    .s32(head.unused14 ?? 0)   // v14: stored, never consumed
    .u8(0)                     // top3_cnt: 0x38-row list empty
    .u8(0);                    // top10_cnt: 0x38-row list empty
}
