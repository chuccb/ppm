/**
 * 994 GG_ASSISTPOINT_NOTIFY (consumer sub_5676D0 @161939, 行級 2026-09-19).
 *
 * Wire:
 *   `u8 eventType`
 *   when eventType != 0:
 *     when eventType < 100: `u8 messageContext, s32 v18`
 *       (v18 is native read-but-unused — init 0, wire-read, then never
 *        referenced again (行級: 函式內 v18 僅 declaration/init/讀入三處);
 *        emitted as the honest 0 here)
 *     `u8 count`
 *     count x { `u8 slot, s32 charId, s32 assistValue, s32 occupyPoint` }
 *
 * Client-side semantics per record (行級):
 *  - `slot`: player-list slot via sub_67D8F0(slot, 0); receive point goes
 *    to (ppt + 60194) — the same assist/occupy slot family as 907 and
 *    the 1010 incremental assign;
 *  - `charId`: compared against own char id (dword_EE8CB4); a self-match
 *    writes the own-score global dword_EE8D24 = assistValue, repaints
 *    the HUD cell, and plays `ui\sounds\assist.wav`;
 *  - `assistValue`: lands at (ppt + 31) and, on self-match, drives the
 *    own-score HUD and the (36, 23) popup via sub_92EF00;
 *  - `occupyPoint`: lands at (ppt + 60194).
 *
 * `eventType` is the native assist-event code (PACKETS.md §2, sub_6750B0
 * cond36 mapping, guard `!= 0 && < 0x6D`); the official domain is named
 * in `AssistEvent` below. Three fixed points on top (行級 sub_5676D0):
 * 0 means bare/no records at all; < 100 gates the message-context pair;
 * 3 (ASSIST_HP) changes the popup condition of unknown_libname_146
 * (self-or-teammate only); 107 (OCCUPY) with a self charId match fires
 * the (32, 23) banner popup.
 *
 * `messageContext` feeds unknown_libname_146's popup message context
 * (native default -1 when the wire does not carry it, i.e. eventType
 * >= 100). Emitted only when eventType < 100. Native stack grants at
 * most 16 records per frame (v16[64] = 4 dwords per record); this
 * builder mirrors that bound defensively.
 *
 * This module is grammar-complete but currently unsent by the server:
 * there is no assist-feeding game mode in this deployment, so no caller
 * fabricates assist events.
 */

import { Packet } from "../../packet.ts";

export interface AssistPointEntry {
  readonly slot: number;
  readonly charId: number;
  readonly assistValue: number;
  readonly occupyPoint: number;
}

export interface AssistPointOptions {
  /** Popup context for unknown_libname_146; native reads it only when eventType < 100. */
  readonly messageContext?: number;
  readonly entries?: readonly AssistPointEntry[];
}

const MAX_ASSIST_RECORDS = 16; // native v16[64] / 4 dwords per record

/**
 * Native assist-event domain (sub_6750B0 cond36 mapping, PACKETS.md §2;
 * deliberately non-contiguous, guard upper bound 0x6D). 108 is the legal
 * ceiling of this client revision.
 */
export const AssistEvent = {
  None: 0,
  AssistDamage: 1,
  AssistAirshot: 2,
  AssistHp: 3,
  BombPlant: 101,
  BombExplo: 102,
  BombDestroy: 103,
  Dye: 104,
  Pulp: 105,
  PulpDestroy: 106,
  Occupy: 107,
  Goal: 108,
} as const;
export type AssistEventType = (typeof AssistEvent)[keyof typeof AssistEvent];

export default function GG_ASSISTPOINT_NOTIFY(
  op: number,
  eventType: number,
  options: AssistPointOptions = {},
): Packet {
  const p = new Packet(op).u8(eventType);
  if (eventType === 0) return p;

  const { messageContext, entries = [] } = options;
  if (eventType < 100) {
    if (messageContext === undefined) {
      throw new RangeError(
        "994 with eventType < 100 needs `messageContext` (native reads the popup-context byte unconditionally)",
      );
    }
    p.u8(messageContext).s32(0); // v18: native read-but-unused
  }

  if (entries.length > MAX_ASSIST_RECORDS) {
    throw new RangeError(
      `994 supports at most ${MAX_ASSIST_RECORDS} records per frame (native v16 bound), got ${entries.length}`,
    );
  }

  p.u8(entries.length);
  for (const entry of entries) {
    p.u8(entry.slot)
      .s32(entry.charId)
      .s32(entry.assistValue)
      .s32(entry.occupyPoint);
  }
  return p;
}
