/**
 * 483 -> 484 GG_GAMECENTER_GAME_START_OK_ACK (consumer sub_584F70,
 * fully re-read line level 2026-09-19; no arms, all nine bytes read).
 *
 *   u16 v8   stored-only word
 *   u8  v7   stored-only byte
 *   u16 v5   -> `sub_5392A0(byte_EE8968, v5, ...)` native slot +196
 *   s32 v6   -> `..."(..., v6)` native slot +197
 *
 * `sub_5392A0` is a plain dual-slot store (`*(this+196)=a2,
 * *(this+197)=a3`) on the game-center record — the same setter family
 * the MYINFO GP word flows into; the semantic owner of slots 196/197
 * stays unresolved, so names here describe the wiring, not a guessed
 * business meaning.
 */

import { Packet } from "../../packet.ts";

export default function GG_GAMECENTER_GAME_START_OK_ACK(
  op: number,
  gameId: number,
  /** v5: stored into game-center slot +196 (u16); defaults to the echoed game id. */
  slot196: number = gameId,
  /** v6: stored into game-center slot +197 (s32). */
  slot197 = 0,
): Packet {
  return new Packet(op)
    .u16(gameId)   // v8: echo word, stored-only
    .u8(1)         // v7: stored-only byte (kept at the proven frame value 1)
    .u16(slot196)  // v5 -> slot +196
    .s32(slot197); // v6 -> slot +197
}
