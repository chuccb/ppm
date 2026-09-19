/**
 * 906 GG_OCC_FAIL_REQ — occupy-object interruption report (builder
 * sub_565470 @160670, 行級 2026-09-19 re-read; naming anchored to the
 * PACKETS.md §3.15d3a REQ→case→ACK cross-verification of the 902/904/906
 * triple).
 *
 * Wire: exactly 6 bytes — `u8 pointId, u8 claimedSlot, s32 claimedUserId`:
 *  - `pointId`: wire emits `*(entity + 64) + 1` — the 1-based form of the
 *    occupy-object controller field; the native ACK parsers guard
 *    `pointId - 1 < 3`, so legal wire values are **1..3**;
 *  - `claimedSlot`: raw own player slot — literal 254 when the client is
 *    in spectator/bot mode (n2 == 2), otherwise own n0x10 (0-based, NO
 *    +1; the native builder is asymmetric by design);
 *  - `claimedUserId`: own character uid (dword_EE8CB4).
 *
 * Call-site context (行級): fired from the entity handler on transitioning
 * *(entity + 3) = 3 — the interrupted-while-capturing state.
 *
 * TS policy: this deployment runs no occupy/hijack game mode and keeps no
 * controller state, so the honest answer is the documented failure arm —
 * action = 0 (the interruption is acknowledged, nothing is granted),
 * points = 0 (no points granted — fact), and the rest echoed from the
 * request itself rather than fabricated:
 *  - `point`: the point the client says it was interrupted at;
 *  - `actorSlot`: the client's own reported slot — the native notice
 *    compares it against the client's own slot to decide whether to
 *    display the interruption banner, so echoing it projects "you were
 *    interrupted"; the spectator 254 form compares against -2 and can
 *    never match, so the slot is projected as 0 there (documented, and
 *    nothing on the client reads it in that state anyway);
 *  - `actorUid`: the reporting client's own uid (§3.15d3a); the client
 *    reads-but-never-uses it (v11), but echoing the fact is more honest
 *    than emitting a fabricated constant.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GG_OCC_FAIL_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 6) {
    throw new RangeError(
      `906 expects exactly 6 bytes (u8 pointId, u8 claimedSlot, s32 claimedUserId), got ${r.remaining}`,
    );
  }
  const pointId = r.u8();
  const claimedSlot = r.u8();
  const claimedUserId = r.s32();

  const slot = claimedSlot === 254 ? 0 : claimedSlot;
  connection.reply("GG_OCC_FAIL_ACK", 0, pointId, slot, 0, claimedUserId);
}
