/**
 * 472 -> 473 GL_GAMECENTER_REC_ACK (consumer sub_584910, fully
 * re-read line level 2026-09-19).
 *
 * Native read order and per-field consumption:
 *
 *   u16 game_id        echo of the record board (stored, not reused here)
 *   s32 v22            read into a local that is never referenced again
 *   u8  top3_cnt       count of 0x38-byte rows placed in the first list
 *   u8  top10_cnt      same for the second list (native cap 0xA check
 *                      happens through the queue resize below)
 *   u8  v24            -> `sub_4574F0(dword_EA1260, v24)` list-control
 *                      state selector (same helper as 481's state byte)
 *   u8  v35            gate: non-zero appends one 0x20-byte raw block
 *                      (block IS passed on to `sub_5384E0`)
 *   u16 v28            stored-only word
 *   s32 v30            -> `sub_5384E0(..., v30)` final board argument
 *   raw 0x10 (v27)     stored-only 16-byte blob
 *   u8  v23            gate: non-zero appends one 0x2C-byte raw block
 *                      (block's first word feeds `sub_4122F0(sub_411A30(), v8)`)
 *   u8  v31            stored-only byte
 *   u16 v32            its low word goes to `sub_5384E0` as v32[0]
 *   u16 v21            -> `sub_5392D0(byte_EE8968, v21, HIBYTE(v21))`:
 *                      low byte -> slot 200, HIGH byte -> slot 201, i.e.
 *                      one wire word packs two one-byte board cells
 *
 * Counts zero pass the <=3 / <=0xA guards. The default frame is the
 * semantic empty record board (no rows, default list state, stored-only
 * cells zero); consumed cells below are open for a game-center store.
 */

import { Packet } from "../../packet.ts";

/** Provably consumed head cells of the record board (v-names keep the native mapping). */
export interface RecordBoardHead {
  /** v24 -> sub_4574F0 list-control state. */
  readonly listState?: number;
  /** v30 -> sub_5384E0 final board argument. */
  readonly boardScore?: number;
  /** v32 -> sub_5384E0 as v32[0]. */
  readonly boardWord?: number;
  /** v21 -> slots 200 (low byte) / 201 (high byte) via sub_5392D0. */
  readonly packedCellPair?: number;
}

export default function GL_GAMECENTER_REC_ACK(
  op: number,
  gameId: number,
  head: RecordBoardHead = {},
): Packet {
  const p = new Packet(op)
    .u16(gameId)
    .s32(0)                         // v22: read but stored-only
    .u8(0)                          // top3_cnt: 0x38-row list empty
    .u8(0)                          // top10_cnt: 0x38-row list empty
    .u8(head.listState ?? 0)        // v24 -> sub_4574F0 list state
    .u8(0)                          // v35: raw-0x20 block gate closed
    .u16(0)                         // v28: stored-only
    .s32(head.boardScore ?? 0);     // v30 -> sub_5384E0 board argument
  for (let i = 0; i < 16; i++) p.u8(0); // 0x10 raw: stored-only blob
  return p
    .u8(0)                          // v23: raw-0x2C block gate closed
    .u8(0)                          // v31: stored-only
    .u16(head.boardWord ?? 0)       // v32 -> sub_5384E0 as v32[0]
    .u16(head.packedCellPair ?? 0); // v21 -> slots 200/201 via sub_5392D0
}
