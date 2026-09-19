/**
 * 907 GG_OCC_FAIL_ACK (consumer sub_565560 @158826+; 行級 2026-09-19 re-read,
 * naming anchored to PACKETS.md §3.15d3a REQ→case→ACK cross-verification).
 *
 * Wire: `u8 action, u8 point, u8 actorSlot, u8 points[, s32 actorUid]`;
 * the trailing s32 is consumed ONLY when action == 0 — read into the
 * native `v11`, which行級 never references again (v11 appears exactly
 * three times: declaration, init 0, read). §3.15d3a cross-verified its
 * server-side meaning as the acting player's uid (mirror of the 906
 * request's own uid column); the TS fail arm therefore echoes the
 * requester's uid instead of projecting a fabricated 0.
 *
 * Client-side semantics (行級):
 *  - action != 0 (success): four bytes only. Hijack mode (sub_67F2F0)
 *    updates controller row `point-1` with `points` via sub_771380;
 *    action == 15 with a matching own actorSlot additionally flashes
 *    the sub-slot row (sub_7713C0). Occupy mode (sub_67F380) fires the
 *    sub_7784D0/778510 pair with actorSlot as the sub-index. Native
 *    guards the controller rows to points 1..3 (`point-1 < 3`).
 *  - action == 0 (failure): reads the s32 actorUid, then shows the
 *    native「capture interrupted, 獲得point=%d」notice (Buffer via msg)
 *    only when actorSlot equals the client's own slot (spectator
 *    compares against -2, i.e. never); hijack mode also repaints the
 *    `point-1` row via sub_771670.
 */

import { Packet } from "../../packet.ts";

export default function GG_OCC_FAIL_ACK(
  op: number,
  action: number,
  point: number,
  actorSlot: number,
  points: number,
  actorUid: number,
): Packet {
  const p = new Packet(op)
    .u8(action)
    .u8(point)
    .u8(actorSlot)
    .u8(points);
  if (action === 0) p.s32(actorUid); // read-but-unused client-side; §3.15d3a semantic echo
  return p;
}
