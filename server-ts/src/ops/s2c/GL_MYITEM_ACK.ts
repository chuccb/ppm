/**
 * 199 -> 200 inventory page (consumer `sub_570AB0`: `s8/bool success`
 * gate, nonzero forwards to the `sub_524B70` record loop).
 *
 * Native store layout in the 5120-slot table (sub_524B70, line level
 * 2026-09-19; all offsets are DWORD-indexed, record stride 7 dwords at
 * base 210):
 * - slot       -> +210+7i (echoed out by the sub_74E390 serializer)
 * - item id    -> +211+7i (`sub_535020` catalog check up front;
 *   failure is the native **error code 6** via `sub_528960(6,...)`,
 *   not the 527550-path error 10)
 * - f1/f2      -> +212/+213+7i — a full base-address sweep shows BOTH
 *   cells are written ONLY here and never read by any other code in
 *   the client image: proven stored-only (the "float?" question is
 *   answered negatively; this build just ignores them)
 * - period     -> +214+7i — the only live counter: `sub_526E80` /
 *   `sub_528D20` set it per item id, and the outbound serializer keeps
 *   records in the special item-id ranges ONLY when period > 0;
 *   ordinary ids serialize whenever id > 0. Behaviourally a
 *   remaining-duration/activity counter.
 * - extra      -> +215+7i, read only when the a3 gate is set
 *   (a3 = 1 for the MYITEM page) — same base-address sweep result as
 *   f1/f2: stored-only.
 * - durability -> u16 into the separate `28*i+862` DWORD-indexed table;
 *   the `14*i+431/432` pair mirrors current/max, and `sub_534450`
 *   refreshes the client item catalog's durability words from it.
 *
 * The current Store has no inventory/catalog model. An empty successful page is
 * nevertheless a complete, client-consumable response: start index 0 followed
 * immediately by the documented negative-slot sentinel.
 */

import { Packet } from "../../packet.ts";

export interface InvItem {
  readonly slot: number;
  readonly itemId: number;
  /** raw4 wire, stored into +212+7i, never read anywhere (stored-only);
   * finite-f32 projection kept solely for byte compatibility of the API. */
  readonly f1: number;
  /** raw4 wire, stored into +213+7i, never read anywhere (stored-only). */
  readonly f2: number;
  readonly period: number;
  /** u8 after the period; native stores it into +215+7i on the a3=1 page
   * and no code path ever reads it again (stored-only). */
  readonly extra?: number;
  /** Native u16 current/max durability word. */
  readonly durability: number;
}

export default function GL_MYITEM_ACK(op: number, items: readonly InvItem[] = []): Packet {
  const p = new Packet(op).s8(1).s32(0);
  for (const item of items) {
    // A negative slot is the native end-of-page sentinel, not a record value.
    // Native 200 reads both slots with sub_592AC0 (generic raw4), not the
    // typed sub_592B40 f32 reader. f32() here is only a byte-compatible
    // projection for the current number-based API.
    p.s32(item.slot)
      .s32(item.itemId)
      .f32(item.f1)
      .f32(item.f2)
      .s32(item.period)
      .u8(item.extra ?? 0) // native u8 after the period; domain unresolved
      .u16(item.durability); // native u16 current/max durability word
  }
  return p.s32(-1);
}
