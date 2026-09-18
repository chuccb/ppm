/**
 * 935 -> 936 GR_AI_FEVER_START_ACK (consumer sub_7623A0, full-body
 * audit 2026-09-19).
 *
 * Wire: `u8 status, u8 flag, s32 duration_ms, u8 type` — 7 bytes,
 * always. Verified per-field in the declined arm this server uses
 * (status = 0):
 * - `flag`: passed to sub_61FC50 UI-notify -> sub_67D7D0 validates it
 *   strictly inside [16, 76); value 0 is INVALID there, so the notify
 *   branch is skipped entirely. flag = 0 therefore means "no fever
 *   event" — provably inert, not an arbitrary filler.
 * - `duration_ms`: compared against the client's own baseline
 *   dword_EE8CB4; a mismatch only logs. Zero mismatches non-trivially.
 * - `type`: consumed ONLY in the status != 0 AND baseline-match arm — unread
 *   on the declined arm.
 * status != 0 with the baseline match enters the real
 * sub_75E9B0 fever start; this server cannot fabricate that, so the
 * dormant declined frame (seven zero bytes) is the only emission.
 */

import { Packet } from "../../packet.ts";

/** sub_67D7D0 low bound: nothing below 16 can ever be a UI consent event. */
const FEVER_EVENT_NONE = 0;

export default function GR_AI_FEVER_START_ACK(
  op: number,
  status: 0 = 0,
  flag = FEVER_EVENT_NONE,
  durationMs = 0,
  type = 0,
): Packet {
  if (status !== 0 || flag !== FEVER_EVENT_NONE || durationMs !== 0 || type !== 0) {
    throw new RangeError("936 only the dormant declined frame (7 x 0) is safe without a fever model");
  }
  return new Packet(op).u8(0).u8(0).s32(0).u8(0);
}
